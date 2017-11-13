using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        private static async Task setMetadata(Photo photo, InPlaceBitmapMetadataWriter dest) {
            var source = await photo.Dispatcher.InvokeAsync(() => (
                photo.Title,
                photo.Photographer,
                photo.DateTaken,
                photo.Location));
            if (source.Title != null) {
                dest.Title = source.Title;
            }
            if (source.Photographer != null) {
                dest.Author = new ReadOnlyCollection<string>(new string[] { source.Photographer });
            }
            if (source.DateTaken.HasValue) {
                dest.SetQuery(DateTakenQuery,
                    source.DateTaken.Value.ToString(ExifDateFormat, CultureInfo.InvariantCulture));
            }
            if (source.Location != null) {
                setLocation(dest, source.Location);
            }
        }

        public static async Task Commit(Photo photo) {
            using (var mmap = MemoryMappedFile.OpenExisting(
                mmapName(photo.FileName),
                MemoryMappedFileRights.ReadWrite)) {
                using (var stream = new UnsafeMemoryMapStream(
                            mmap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite),
                            FileAccess.ReadWrite)) {
                    var decoder = JpegBitmapDecoder.Create(stream.Stream,
                                BitmapCreateOptions.PreservePixelFormat,
                                BitmapCacheOption.OnDemand);
                    var frames = decoder.Frames;
                    if (frames.Count < 1) {
                        throw new ArgumentException("Image contained no frame data.", nameof(photo));
                    }
                    var md = frames[0].CreateInPlaceBitmapMetadataWriter();
                    await setMetadata(photo, md);
                }
            }
        }

        #region EXIF constants

        const string ExifDateFormat = "yyyy:MM:dd HH:mm:ss";

        const string DateTakenQuery = "/app1/ifd/exif/{ushort=36867}";
        const string DateTakenSubsecQuery = "/app1/ifd/exif/{ushort=37521}";
        const string OrientationQuery = "/app1/ifd/{ushort=274}";

        const string LatitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=1}";
        const string LatitudeQuery = "/app1/ifd/gps/subifd:{ulong=2}";
        const string LongitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=3}";
        const string LongitudeQuery = "/app1/ifd/gps/subifd:{ulong=4}";

        #endregion

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

        private static GpsLocation readLocation(BitmapMetadata metadata) {
            var latSignProp = metadata.GetQuery(LatitudeRefQuery) as string;
            var latProp = metadata.GetQuery(LatitudeQuery) as ulong[];
            var lonSignProp = metadata.GetQuery(LongitudeRefQuery) as string;
            var lonProp = metadata.GetQuery(LongitudeQuery) as ulong[];
            if (latSignProp == null || latProp == null || lonSignProp == null || lonProp == null) {
                return null;
            }
            if (latSignProp.Length != 1 || lonSignProp.Length != 1 ||
                latProp.Length != 3 || lonProp.Length != 3) {
                return null;
            }
            return GpsLocation.FromBytes(
                Encoding.ASCII.GetBytes(latSignProp),
                latProp.SelectMany(BitConverter.GetBytes).ToArray(),
                Encoding.ASCII.GetBytes(lonSignProp),
                lonProp.SelectMany(BitConverter.GetBytes).ToArray());
        }

        private static IEnumerable<ulong> bytesToLongs(byte[] from) {
            for (int i = 0; i < from.Length; i += sizeof(ulong)) {
                yield return (ulong)BitConverter.ToInt64(from, i);
            }
        }

        private static void setLocation(InPlaceBitmapMetadataWriter dest, GpsLocation loc) {
            dest.SetQuery(LatitudeRefQuery, Encoding.ASCII.GetString(loc.LatSignBytes));
            dest.SetQuery(LatitudeQuery, bytesToLongs(loc.LatBytes).ToArray());
            dest.SetQuery(LongitudeRefQuery, Encoding.ASCII.GetString(loc.LonSignBytes));
            dest.SetQuery(LongitudeQuery, bytesToLongs(loc.LonBytes).ToArray());
        }

        static string maybeGetString(BitmapMetadata metadata, string query) {
            return metadata.GetQuery(query)?.ToString();
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
