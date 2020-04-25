using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PhotoTagger.Imaging {
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
        const string XmpTitleQuery = "/xmp/dc:title";
        const string XmpDescriptionQuery = "/xmp/dc:description";
        const string DateTakenQuery = "/app1/ifd/exif/{ushort=36867}";
        const string DateTakenSubsecQuery = "/app1/ifd/exif/{ushort=37521}";
        const string JpegOrientationQuery = "/app1/ifd/{ushort=274}";
        const string RawOrientationQuery = "/ifd/{ushort=274}";

        const string LatitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=1}";
        const string LatitudeQuery = "/app1/ifd/gps/subifd:{ulong=2}";
        const string LongitudeRefQuery = "/app1/ifd/gps/subifd:{ulong=3}";
        const string LongitudeQuery = "/app1/ifd/gps/subifd:{ulong=4}";

#pragma warning disable IDE0051 // Remove unused private members
        const string PaddingQuery = "/app1/ifd/PaddingSchema:Padding";
        const string ExifPaddingQuery = "/app1/ifd/exif/PaddingSchema:Padding";
        const string XmpPaddingQuery = "/xmp/PaddingSchema:Padding";
        const string ColorSpaceQuery = "/app1/{ushort=0}/{ushort=34665}/{ushort=40961}";
#pragma warning restore IDE0051 // Remove unused private members

        // From the System.Title Photo Metadata Policy
        readonly static string[] TitleReadQueries = {
            WinTitleQuery,
            "/xmp/<xmpalt>dc:title",
            XmpTitleQuery,
            "/app1/ifd/exif/{ushort=37510}",
            TitleQuery,
            "/app13/irb/8bimiptc/iptc/caption",
            "/xmp/<xmpalt>dc:description",
            XmpDescriptionQuery,
            "/app13/irb/8bimiptc/iptc/caption",
            "/xmp/<xmpalt>exif:UserComment",
        };

        // From the System.Title Photo Metadata Policy
        readonly static string[] TitleRemoveQueries = {
            WinTitleQuery,
            XmpTitleQuery,
            "/app1/ifd/exif/{ushort=37510}",
            "/xmp/<xmpalt>exif:UserComment",
            TitleQuery,
            "/app13/irb/8bimiptc/iptc/caption",
            XmpDescriptionQuery,
        };

        // From the System.Author Photo Metadata Policy
        readonly static string[] AuthorRemoveQueries = {
            "/xmp/dc:creator",
            "/xmp/tiff:artist",
            "/app13/irb/8bimiptc/iptc/by-line",
            "/app1/ifd/{ushort=315}",
            "/app1/ifd/{ushort=40093}",
        };

        private static readonly ReadOnlyCollection<string> EmptyStringCollection =
            new ReadOnlyCollection<string>(new string[] { });

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

        private static string? readString(BitmapMetadata metadata, string key) {
            var md = metadata.GetQuery(key);
            switch (md) {
                case string direct:
                    if (key.StartsWith("/xmp/")) {
                        return direct;
                    } else {
                        return Encoding.UTF8.GetString(Encoding.Default.GetBytes(direct));
                    }
                case BitmapMetadata indirect:
                    foreach (string query in indirect) {
                        return readString(indirect, key + query);
                    }
                    return null;
                case byte[] bytes:
                    return Encoding.Unicode.GetString(bytes);
                default:
                    return null;
            }
        }

        private static string readTitle(BitmapMetadata metadata) {
            foreach (var query in TitleReadQueries) {
                var v = readString(metadata, query);
                if (!string.IsNullOrWhiteSpace(v)) {
                    return fromUniversalNewline(v)
                        .TrimEnd('\0').Trim();
                }
            }
            return string.Empty;
        }

        private static string? readAuthor(BitmapMetadata metadata) {
            return metadata.Author?.FirstOrDefault();
        }

        private static DateTime? readDateTaken(BitmapMetadata metadata) {
            var dateString = readString(metadata, DateTakenQuery);
            if (string.IsNullOrWhiteSpace(dateString)) {
                return null;
            }
            if (!DateTime.TryParseExact(dateString,
                ExifDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) {
                return null;
            }
            var subsecString = readString(metadata, DateTakenSubsecQuery);
            if (subsecString != null && double.TryParse("0." + subsecString,
                                                        NumberStyles.Float,
                                                        CultureInfo.InvariantCulture,
                                                        out double subsec)) {
                d = d.AddSeconds(subsec);
            }
            return d;
        }

        private static GpsLocation? readLocation(BitmapMetadata metadata) {
            if (!(metadata.GetQuery(LatitudeRefQuery) is string latSignProp) ||
                !(metadata.GetQuery(LatitudeQuery) is ulong[] latProp) ||
                !(metadata.GetQuery(LongitudeRefQuery) is string lonSignProp) ||
                !(metadata.GetQuery(LongitudeQuery) is ulong[] lonProp)) {
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

        public static Rotation OrienationToRotation(short orienation) {
            return orienation switch
            {
                1 => Rotation.Rotate0,
                3 => Rotation.Rotate180,
                6 => Rotation.Rotate90,
                8 => Rotation.Rotate270,
                _ => throw new NotSupportedException("Unsupported exif rotation."),
            };
        }

        private static short getOrientation(BitmapMetadata metadata) {
            try {
                ushort? orientationProp;
                if (metadata.ContainsQuery(JpegOrientationQuery)) {
                    orientationProp = metadata.GetQuery(JpegOrientationQuery) as ushort?;
                } else if (metadata.ContainsQuery(RawOrientationQuery)) {
                    orientationProp = metadata.GetQuery(RawOrientationQuery) as ushort?;
                } else {
                    return 1;
                }
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
                source.Orientation = 1;
            });
            if (!string.IsNullOrWhiteSpace(source.Title)) {
                var title = toUniversalNewline(source.Title.Trim());
                var bytes = Encoding.UTF8.GetBytes(title);
                dest.SetQuery(TitleQuery, Encoding.Default.GetString(bytes));
                var utf16bytes = Encoding.Unicode.GetBytes(title + '\0');
                dest.SetQuery(WinTitleQuery, utf16bytes);
                dest.Title = title;
            } else {
                dest.Title = string.Empty;
                foreach (var query in TitleRemoveQueries) {
                    dest.RemoveQuery(query);
                }
            }
            if (!string.IsNullOrWhiteSpace(source.Author)) {
                var bytes = Encoding.UTF8.GetBytes(source.Author);
                dest.Author = new ReadOnlyCollection<string>(new string[] {
                    Encoding.Default.GetString(bytes)
                });
            } else {
                dest.Author = EmptyStringCollection;
                foreach (var query in AuthorRemoveQueries) {
                    dest.RemoveQuery(query);
                }
            }
            if (source.DateTaken.HasValue) {
                var bytes = Encoding.ASCII.GetBytes(
                    source.DateTaken.Value.ToString(ExifDateFormat, CultureInfo.InvariantCulture));
                dest.SetQuery(DateTakenQuery, Encoding.Default.GetString(bytes));
            } else {
                dest.RemoveQuery(DateTakenQuery);
                dest.RemoveQuery(DateTakenSubsecQuery);
            }
            if (source.Location != null) {
                setLocation(dest, source.Location);
            } else {
                clearLocation(dest);
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

        private static void clearLocation(BitmapMetadata dest) {
            dest.RemoveQuery(LatitudeRefQuery);
            dest.RemoveQuery(LatitudeQuery);
            dest.RemoveQuery(LongitudeRefQuery);
            dest.RemoveQuery(LongitudeQuery);
        }

        internal static Rotation SaveRotation(BitmapMetadata md) {
            var rotation = OrienationToRotation(getOrientation(md));
            if (rotation != Rotation.Rotate0) {
                md.SetQuery(JpegOrientationQuery, (short)1);
            }
            return rotation;
        }

        #endregion
    }
}
