using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoTagger {
    class ImageLoadManager {
        private static readonly ImageCodecInfo JpegCodec =
            ImageCodecInfo.GetImageEncoders()
            .First(enc => enc.MimeType == "image/jpeg");

        public void EnqueueLoad(Photo photo,
                                ObservableCollection<Photo> list) {
            metadataReads.Add(new Tuple<Photo, ObservableCollection<Photo>>(photo, list));
            makeIOThread();
        }

        private BlockingCollection<Tuple<Photo, ObservableCollection<Photo>>> metadataReads =
            new BlockingCollection<Tuple<Photo, ObservableCollection<Photo>>>();

        private BlockingCollection<Tuple<Photo, Photo.Metadata>> fullsizeReads =
            new BlockingCollection<Tuple<Photo, Photo.Metadata>>();

        private static readonly int MaxIOThreads = Math.Min(Environment.ProcessorCount, 3);
        private int runningIOThreads = 0;

        private void makeIOThread() {
            if (runningIOThreads < MaxIOThreads) {
                ThreadPool.QueueUserWorkItem(ioWorker);
            }
        }

        private async void ioWorker(object state) {
            while (Interlocked.Increment(ref runningIOThreads) > MaxIOThreads) {
                if (Interlocked.Decrement(ref runningIOThreads) >= MaxIOThreads) {
                    return;
                }
            }
            try {
                while (metadataReads.Count > 0 || fullsizeReads.Count > 0) {
                    if (metadataReads.TryTake(out Tuple<Photo, ObservableCollection<Photo>> meta)) {
                        await loadMeta(meta.Item1, meta.Item2);
                    } else if (fullsizeReads.TryTake(out Tuple<Photo, Photo.Metadata> photo)) {
                        await setImage(photo.Item1, photo.Item2);
                    }
                }
            } finally {
                Interlocked.Decrement(ref runningIOThreads);
            }
            if (metadataReads.Count > 0 || fullsizeReads.Count > 0) {
                ThreadPool.QueueUserWorkItem(ioWorker);
            }
        }

        private static string mmapName(string fname) {
            return fname.Replace('\\', '/') + fname.GetHashCode().ToString();
        }

        private async Task loadMeta(Photo photo,
                                    ObservableCollection<Photo> list) {
            try {
                var mmap = MemoryMappedFile.CreateFromFile(photo.FileName,
                    FileMode.Open, mmapName(photo.FileName),
                    0, MemoryMappedFileAccess.Read);
                var img = new BitmapImage();
                Photo.Metadata metadata;
                DispatcherOperation metaSet;
                try {
                    using (var stream = new UnsafeMemoryMapStream(
        mmap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read),
        FileAccess.Read)) {
                        var data = stream.Stream;
                        {
                            var decoder = JpegBitmapDecoder.Create(data,
                                BitmapCreateOptions.PreservePixelFormat,
                                BitmapCacheOption.None);
                            var frames = decoder.Frames;
                            if (frames.Count < 1) {
                                throw new ArgumentException("Image contained no frame data.", nameof(photo));
                            }
                            var imgMeta = frames[0].Metadata as BitmapMetadata;
                            if (imgMeta == null) {
                                throw new NullReferenceException("Image contained no metadata");
                            }
                            metadata = getMetadata(imgMeta);
                            metadata.Width = frames[0].PixelWidth;
                            metadata.Height = frames[0].PixelHeight;
                        }

                        data.Seek(0, SeekOrigin.Begin);
                        metaSet = photo.Dispatcher.InvokeAsync(() => photo.Set(metadata));
                        img.BeginInit();
                        img.StreamSource = data;
                        img.DecodePixelHeight = 48;
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.Rotation = orienationToRotation(metadata.Orientation);
                        img.EndInit();
                        img.Freeze();
                    }
                    await metaSet;
                } catch {
                    mmap.Dispose();
                    throw;
                }
                if (await photo.Dispatcher.InvokeAsync(() => {
                    if (photo.Disposed) {
                        return false;
                    }
                    photo.mmap = mmap;
                    photo.ThumbImage = img;
                    return true;
                })) {
                    fullsizeReads.Add(new Tuple<Photo, Photo.Metadata>(photo, metadata));
                }
            } catch (Exception ex) {
                await photo.Dispatcher.InvokeAsync(() => {
                    MessageBox.Show(ex.ToString(),
                        string.Format("Error loading {0}\n\n{1}",
                        photo.FileName, ex),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    photo.Dispose();
                    list.Remove(photo);
                });
            }
        }

        private async static Task setImage(Photo photo, Photo.Metadata metadata) {
            bool locked = false;
            try {
                // To avoid dispose colliding with setImage.
                await photo.loadLock.WaitAsync();
                locked = true;
                var data = await photo.Dispatcher.InvokeAsync(
                    () => (photo.mmap, photo.fullImageStream));
                if (data.mmap == null) {
                    // disposed
                    return;
                }
                if (data.fullImageStream == null) {
                    data.fullImageStream = new UnsafeMemoryMapStream(
                        data.mmap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read),
                        FileAccess.Read);
                    await photo.Dispatcher.InvokeAsync(() => {
                        var p = Interlocked.CompareExchange(ref photo.fullImageStream, data.fullImageStream, null);
                        if (p != null) {
                            data.fullImageStream.Dispose();
                            data.fullImageStream = p;
                        }
                    });
                }
                var img = new BitmapImage();
                try {
                    img.BeginInit();
                    img.StreamSource = data.fullImageStream.Stream;
                    makeFullImage(metadata, img);
                } catch (Exception ex) {
                    await photo.Dispatcher.InvokeAsync(() => {
                        MessageBox.Show(ex.ToString(),
                            string.Format("Error loading {0}\n\n{1}",
                            photo.FileName, ex),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                    return;
                }
                await photo.Dispatcher.InvokeAsync(() => {
                    photo.FullImage = img;
                });
            } finally {
                if (locked) {
                    photo.loadLock.Release();
                }
            }
        }

        private static void makeFullImage(Photo.Metadata metadata, BitmapImage img) {
            if (metadata.Width > SystemParameters.MaximizedPrimaryScreenWidth ||
                                metadata.Height > SystemParameters.MaximizedPrimaryScreenHeight) {
                if (metadata.Width * SystemParameters.MaximizedPrimaryScreenHeight >
                    metadata.Height * SystemParameters.MaximizedPrimaryScreenWidth) {
                    img.DecodePixelWidth = (int)SystemParameters.MaximizedPrimaryScreenWidth;
                } else {
                    img.DecodePixelHeight = (int)SystemParameters.MaximizedPrimaryScreenHeight;
                }
            }
            img.CacheOption = BitmapCacheOption.None;
            img.Rotation = orienationToRotation(metadata.Orientation);
            img.EndInit();
            img.Freeze();
        }

        private static Photo.Metadata getMetadata(BitmapMetadata metadata) {
            return new Photo.Metadata() {
                Title = metadata.Title,
                Author = metadata.Author?.FirstOrDefault(),
                DateTaken = readDateTaken(metadata),
                Location = readLocation(metadata),
                Orientation = getOrientation(metadata),
            };
        }

        #region EXIF constants

        const string ExifDateFormat = "yyyy:MM:dd HH:mm:ss";

        const short ExifTypeAscii = 2;
        const short ExifTypeShort = 3;
        const short ExifTypeRational = 5;

        const int TitlePropID = 0x0320;
        const int AuthorPropID = 0x013B;
        const int DateTakenPropID = 0x9003;
        const string DateTakenQuery = "/app1/ifd/exif/{ushort=36867}";
        const int DateTakenSubsecPropID = 0x9291;
        const string DateTakenSubsecQuery = "/app1/ifd/exif/{ushort=37521}";
        const int OrientationPropID = 0x0112;
        const string OrientationQuery = "/app1/ifd/{ushort=274}";
        const int HorizontalResPropID = 0x011A;
        const int VerticalResPropID = 0x011B;
        const int ResolutionUnitPropID = 0x0128;

        const int LatitudeRefPropId = 0x0001;
        const string LatitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=1}";
        const int LatitudePropId = 0x0002;
        const string LatitudeQuery = "/app1/ifd/gps/subifd:{ulong=2}";
        const int LongitudeRefPropId = 0x0003;
        const string LongitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=3}";
        const int LongitudePropId = 0x0004;
        const string LongitudeQuery = "/app1/ifd/gps/subifd:{ulong=4}";

        #endregion

        private static string readTitle(Image bmp) {
            return maybeGetString(bmp, TitlePropID, Encoding.UTF8);
        }

        private static string readAuthor(Image bmp) {
            return maybeGetString(bmp, AuthorPropID, Encoding.UTF8);
        }

        private static DateTime? readDateTaken(Image bmp) {
            var dateString = maybeGetString(bmp, DateTakenPropID, Encoding.ASCII);
            if (dateString == null) {
                return null;
            }
            var d = DateTime.ParseExact(dateString,
                ExifDateFormat, CultureInfo.InvariantCulture);
            var subsecString = maybeGetString(bmp, DateTakenSubsecPropID, Encoding.ASCII);
            if (subsecString != null &&
                double.TryParse("0." + subsecString, out double subsec)) {
                d = d.AddSeconds(subsec);
            }
            return d;
        }

        private static DateTime? readDateTaken(BitmapMetadata metadata) {
            var dateString = maybeGetString(metadata, DateTakenQuery);
            if (string.IsNullOrWhiteSpace(dateString)) {
                return null;
            }
            if (!DateTime.TryParseExact(dateString,
                ExifDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) {
                return null;
            }
            var subsecString = maybeGetString(metadata, DateTakenSubsecQuery);
            if (subsecString != null && double.TryParse("0." + subsecString,
                                                        NumberStyles.Float,
                                                        CultureInfo.InvariantCulture,
                                                        out double subsec)) {
                d = d.AddSeconds(subsec);
            }
            return d;
        }

        private static GpsLocation readLocation(Image bmp) {
            var latSignProp = maybeGetProp(bmp, LatitudeRefPropId);
            var latProp = maybeGetProp(bmp, LatitudePropId);
            var lonSignProp = maybeGetProp(bmp, LongitudeRefPropId);
            var lonProp = maybeGetProp(bmp, LongitudePropId);
            if (latSignProp == null || latProp == null || lonSignProp == null || lonProp == null) {
                return null;
            }
            if (latSignProp.Len != 2 || lonSignProp.Len != 2 ||
                latProp.Len != 24 || lonProp.Len != 24) {
                return null;
            }
            if (latSignProp.Type != ExifTypeAscii || lonSignProp.Type != ExifTypeAscii ||
                latProp.Type != ExifTypeRational || lonProp.Type != ExifTypeRational) {
                return null;
            }
            return GpsLocation.FromBytes(
                latSignProp.Value, latProp.Value,
                lonSignProp.Value, lonProp.Value);
        }

        private static GpsLocation readLocation(BitmapMetadata metadata) {
            var latSignProp = metadata.GetQuery(LatitudeRefQuery) as string;
            var latProp = metadata.GetQuery(LatitudeQuery) as uint[];
            var lonSignProp = metadata.GetQuery(LongitudeRefQuery) as string;
            var lonProp = metadata.GetQuery(LongitudeQuery) as uint[];
            if (latSignProp == null || latProp == null || lonSignProp == null || lonProp == null) {
                return null;
            }
            if (latSignProp.Length != 1 || lonSignProp.Length != 1 ||
                latProp.Length != 6 || lonProp.Length != 6) {
                return null;
            }
            return GpsLocation.FromLongs(
                latSignProp, latProp,
                lonSignProp, lonProp);
        }

        static string maybeGetString(Image bmp, int propID, Encoding enc) {
            var prop = maybeGetProp(bmp, propID);
            if (prop == null) {
                return null;
            }
            if (prop.Type != ExifTypeAscii || prop.Len < 1) {
                return null;
            }
            var s = enc.GetString(prop.Value, 0, prop.Len - 1);
            if (string.IsNullOrWhiteSpace(s)) {
                return null;
            }
            return s;
        }

        static string maybeGetString(BitmapMetadata metadata, string query) {
            return metadata.GetQuery(query)?.ToString();
        }

        static PropertyItem maybeGetProp(Image bmp, int propID) {
            try {
                return bmp.GetPropertyItem(propID);
            } catch (ArgumentException) {
                return null;
            }
        }

        private static async Task readMetadata(Image bmp, Photo photo) {
            var title = readTitle(bmp);
            var author = readAuthor(bmp);
            var date = readDateTaken(bmp);
            var loc = readLocation(bmp);
            if (title != null || author != null || date.HasValue || loc != null) {
                await photo.Dispatcher.InvokeAsync(() => {
                    if (title != null) {
                        photo.Title = title;
                    }
                    if (author != null) {
                        photo.Photographer = author;
                    }
                    if (date.HasValue) {
                        photo.DateTaken = date.Value;
                    }
                    if (loc != null) {
                        photo.Location = loc;
                    }
                });
            }
        }

        private static Rotation orienationToRotation(short orienation) {
            switch (orienation) {
                case 1:
                    return Rotation.Rotate0;
                case 3:
                    return Rotation.Rotate180;
                case 6:
                    return Rotation.Rotate90;
                case 8:
                    return Rotation.Rotate270;
                default:
                    throw new NotSupportedException("Unsupported exif rotation.");
            }
        }

        private static short getOrientation(Image bmp) {
            try {
                var orientationProp = bmp.GetPropertyItem(OrientationPropID);
                if (orientationProp.Type == ExifTypeShort &&
                    orientationProp.Len == 2) {
                    return BitConverter.ToInt16(orientationProp.Value, 0);
                }
            } catch (ArgumentException) {
                return 1;
            }
            return 1;
        }

        private static short getOrientation(BitmapMetadata metadata) {
            try {
                var orientationProp = metadata.GetQuery(OrientationQuery) as ushort?;
                if (orientationProp.HasValue) {
                    return (short)orientationProp.Value;
                }
            } catch (ArgumentException) {
                return 1;
            }
            return 1;
        }
    }
}
