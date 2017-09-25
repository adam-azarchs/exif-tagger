using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoTagger {
    class ImageLoadManager {
        private static readonly ImageCodecInfo JpegCodec =
            ImageCodecInfo.GetImageEncoders()
            .First(enc => enc.MimeType == "image/jpeg");

        public void EnqueueLoad(Photo photo,
                                ObservableCollection<Photo> list) {
            Task.Run(() => loadImage(photo, list));
        }

        private static async void loadImage(Photo photo,
                                     ObservableCollection<Photo> list) {
            try {
                using (var mem = new MemoryStream()) {
                    using (var f = File.OpenRead(photo.FileName)) {
                        await f.CopyToAsync(mem);
                    }
                    mem.Seek(0, SeekOrigin.Begin);
                    short orientation;
                    System.Drawing.Size size;
                    using (var bmp = Bitmap.FromStream(mem, true, true)) {
                        await readMetadata(bmp, photo);
                        orientation = getOrientation(bmp);
                        size = bmp.Size;
                    }
                    mem.Seek(0, SeekOrigin.Begin);
                    await setImage(mem, orientation, size.Width, size.Height, photo);
                }
            } catch (Exception ex) {
                await photo.Dispatcher.InvokeAsync(() => {
                    MessageBox.Show(ex.ToString(),
                        string.Format("Error loading {0}\n\n{1}",
                        photo.FileName, ex),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    list.Remove(photo);
                });
            }
        }

        const string ExifDateFormat = "yyyy:MM:dd HH:mm:ss";

        const short ExifTypeAscii = 2;
        const short ExifTypeShort = 3;
        const short ExifTypeRational = 5;

        const int TitlePropID = 0x0320;
        const int AuthorPropID = 0x013B;
        const int DateTakenPropID = 0x9003;
        const int DateTakenSubsecPropID = 0x9291;
        const int OrientationPropID = 0x0112;
        const int HorizontalResPropID = 0x011A;
        const int VerticalResPropID = 0x011B;
        const int ResolutionUnitPropID = 0x0128;
        const int LatitudeRefPropId = 0x0001;
        const int LatitudePropId = 0x0002;
        const int LongitudeRefPropId = 0x0003;
        const int LongitudePropId = 0x0004;

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

        private async static Task setImage(Stream data,
            short orienation, int width, int height,
            Photo photo) {
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = data;
            if (width > SystemParameters.MaximizedPrimaryScreenWidth ||
                height > SystemParameters.MaximizedPrimaryScreenHeight) {
                if (width * SystemParameters.MaximizedPrimaryScreenHeight >
                    height * SystemParameters.MaximizedPrimaryScreenWidth) {
                    img.DecodePixelWidth = (int)SystemParameters.MaximizedPrimaryScreenWidth;
                } else {
                    img.DecodePixelHeight = (int)SystemParameters.MaximizedPrimaryScreenHeight;
                }
            }
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.Rotation = orienationToRotation(orienation);
            img.EndInit();
            img.Freeze();
            await photo.Dispatcher.InvokeAsync(() => {
                photo.CurrentDisplayImage = img;
            });
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
    }
}
