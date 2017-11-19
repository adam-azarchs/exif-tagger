using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoTagger {
    static class Exif {

        private static string toUniversalNewline(string from) {
            if (Environment.NewLine == "\n") {
                return from;
            } else {
                return from.Replace(Environment.NewLine, "\n");
            }
        }
        private static string fromUniversalNewline(string from) {
            if (Environment.NewLine == "\n") {
                return from;
            } else {
                return from.Replace("\n", Environment.NewLine);
            }
        }

        #region constants

        const string ExifDateFormat = "yyyy:MM:dd HH:mm:ss";

        const string TitleQuery = "/app1/ifd/{ushort=270}";
        const string WinTitleQuery = "/app1/ifd/{ushort=40091}";
        const string XmpTitleQuery = "/xmp/dc:title/x-default";
        const string XmpDescriptionQuery = "/xmp/dc:description/x-default";
        const string DateTakenQuery = "/app1/ifd/exif/{ushort=36867}";
        const string DateTakenSubsecQuery = "/app1/ifd/exif/{ushort=37521}";
        const string OrientationQuery = "/app1/ifd/{ushort=274}";

        const string LatitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=1}";
        const string LatitudeQuery = "/app1/ifd/gps/subifd:{ulong=2}";
        const string LongitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=3}";
        const string LongitudeQuery = "/app1/ifd/gps/subifd:{ulong=4}";

        const string PaddingQuery = "/app1/ifd/PaddingSchema:Padding";
        const string ExifPaddingQuery = "/app1/ifd/exif/PaddingSchema:Padding";
        const string XmpPaddingQuery = "/xmp/PaddingSchema:Padding";

        #endregion

        #region field readers

        public static Photo.Metadata GetMetadata(BitmapMetadata metadata) {
            return new Photo.Metadata() {
                Title = readTitle(metadata),
                Author = readAuthor(metadata),
                DateTaken = readDateTaken(metadata),
                Location = readLocation(metadata),
                Orientation = getOrientation(metadata),
            };
        }

        private static string readTitle(BitmapMetadata metadata) {
            var xmpTitle = metadata.GetQuery(XmpDescriptionQuery) as string ??
                metadata.GetQuery(XmpTitleQuery) as string;
            if (!string.IsNullOrWhiteSpace(xmpTitle)) {
                return fromUniversalNewline(xmpTitle).Trim();
            }
            var exifTitle = maybeGetString(metadata, TitleQuery);
            if (!string.IsNullOrWhiteSpace(exifTitle)) {
                return fromUniversalNewline(exifTitle).Trim();
            }
            if (metadata.GetQuery(WinTitleQuery) is byte[] winTitle) {
                var winTitleString = Encoding.Unicode.GetString(winTitle);
                if (!string.IsNullOrWhiteSpace(winTitleString)) {
                    // Remove trailing null.
                    return fromUniversalNewline(winTitleString
                        .Substring(0, winTitleString.Length - 1)).Trim();
                }
            }
            return xmpTitle;
        }

        private static string readAuthor(BitmapMetadata metadata) {
            return metadata.Author?.FirstOrDefault();
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

        static string maybeGetString(BitmapMetadata metadata, string query) {
            var data = metadata.GetQuery(query);
            if (data == null) {
                return null;
            } else {
                // metadata.GetQuery interprets the bytes in the default
                // encoding.  This is usually wrong.  Re-decode as UTF8.
                return Encoding.UTF8.GetString(Encoding.Default.GetBytes(data.ToString()));
            }
        }

        public static Rotation OrienationToRotation(short orienation) {
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

        #endregion

        #region field writers

        public static async Task<Photo.Metadata> SetMetadata(Photo photo, BitmapMetadata dest) {
            var source = GetMetadata(dest);
            await photo.Dispatcher.InvokeAsync(() => {
                source.Title = photo.Title;
                source.Author = photo.Photographer;
                source.DateTaken = photo.DateTaken;
                source.Location = photo.Location;
            });
            int pad = 0;
            if (source.Title != null) {
                var title = toUniversalNewline(source.Title.Trim());
                var bytes = Encoding.UTF8.GetBytes(title);
                dest.SetQuery(TitleQuery, Encoding.Default.GetString(bytes));
                var utf16bytes = Encoding.Unicode.GetBytes(title + '\0');
                dest.SetQuery(WinTitleQuery, utf16bytes);
                dest.Title = title;
                pad += bytes.Length * 4 + utf16bytes.Length + 16;
            }
            if (source.Author != null) {
                var bytes = Encoding.UTF8.GetBytes(source.Author);
                dest.Author = new ReadOnlyCollection<string>(new string[] {
                    Encoding.Default.GetString(bytes)
                });
                pad += bytes.Length + 16;
            }
            if (source.DateTaken.HasValue) {
                var bytes = Encoding.ASCII.GetBytes(
                    source.DateTaken.Value.ToString(ExifDateFormat, CultureInfo.InvariantCulture));
                dest.SetQuery(DateTakenQuery, Encoding.Default.GetString(bytes));
                pad += bytes.Length + 16;
            }
            if (source.Location != null) {
                setLocation(dest, source.Location);
                pad += 404;
            }
            if (pad != 0) {
                uint padding = (uint)pad + 256u;
                dest.SetQuery(PaddingQuery, padding);
                dest.SetQuery(ExifPaddingQuery, padding);
                dest.SetQuery(XmpPaddingQuery, padding);
            }
            return source;
        }

        private static IEnumerable<ulong> bytesToLongs(byte[] from) {
            for (int i = 0; i < from.Length; i += sizeof(ulong)) {
                yield return (ulong)BitConverter.ToInt64(from, i);
            }
        }

        private static void setLocation(BitmapMetadata dest, GpsLocation loc) {
            dest.SetQuery(LatitudeRefQuery, Encoding.Default.GetString(loc.LatSignBytes));
            dest.SetQuery(LatitudeQuery, bytesToLongs(loc.LatBytes).ToArray());
            dest.SetQuery(LongitudeRefQuery, Encoding.Default.GetString(loc.LonSignBytes));
            dest.SetQuery(LongitudeQuery, bytesToLongs(loc.LonBytes).ToArray());
        }

        #endregion
    }
}
