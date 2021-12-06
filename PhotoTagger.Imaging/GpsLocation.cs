using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PhotoTagger.Imaging {
    /// <summary>
    /// Stores latitude and longitude information in a form convenient for
    /// loading to saving from or to EXIF metadata.  Immutable once
    /// constructed.
    /// </summary>
    public class GpsLocation : IEquatable<GpsLocation> {
        private readonly RationalDegrees lat;
        private readonly RationalDegrees lon;

        public byte[] LatBytes => lat.ToBytes();
        public byte[] LonBytes => lon.ToBytes();
        public byte[] LatSignBytes => lat.Sign < 0 ? South : North;
        public byte[] LonSignBytes => lon.Sign < 0 ? West : East;

        static readonly byte[] North = { (byte)'N', 0 };
        static readonly byte[] South = { (byte)'S', 0 };
        static readonly byte[] East = { (byte)'E', 0 };
        static readonly byte[] West = { (byte)'W', 0 };

        private static readonly Regex LocRegex = new(
            @"(-?[0-9]+\.?[0-9]*)\s*([nsNS]?)\s*,?\s*(-?[0-9]+\.?[0-9]*)\s*([ewEW]?)",
            RegexOptions.Compiled);

        public GpsLocation(RationalDegrees lat, RationalDegrees lon) {
            this.lat = lat;
            this.lon = lon;
        }

        public static bool TryParse(string value, out GpsLocation? result) {
            result = null;
            var match = LocRegex.Match(value);
            if (match == null || !match.Success || match.Groups.Count != 5) {
                return false;
            }
            if (!decimal.TryParse(match.Groups[1].Value, out decimal lat)) {
                return false;
            }
            if (match.Groups[2].Value == "S" || match.Groups[2].Value == "s") {
                lat *= -1;
            }
            if (!decimal.TryParse(match.Groups[3].Value, out decimal lon)) {
                return false;
            }
            if (match.Groups[4].Value == "W" || match.Groups[4].Value == "w") {
                lon *= -1;
            }
            result = new GpsLocation(
                RationalDegrees.FromDecimal(lat),
                RationalDegrees.FromDecimal(lon));
            return true;
        }

        public static bool TryParse(string value, IFormatProvider provider, out GpsLocation? result) {
            result = null;
            var match = LocRegex.Match(value);
            if (match == null || !match.Success || match.Groups.Count != 5) {
                return false;
            }
            if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Float, provider, out decimal lat)) {
                return false;
            }
            if (match.Groups[2].Value == "S" || match.Groups[2].Value == "s") {
                lat *= -1;
            }
            if (!decimal.TryParse(match.Groups[3].Value, NumberStyles.Float, provider, out decimal lon)) {
                return false;
            }
            if (match.Groups[4].Value == "W" || match.Groups[4].Value == "w") {
                lon *= -1;
            }
            result = new GpsLocation(
                RationalDegrees.FromDecimal(lat),
                RationalDegrees.FromDecimal(lon));
            return true;
        }

        public override string ToString() {
            return lat.ToDouble().ToString() + ", "
                + lon.ToDouble().ToString();
        }

        public string ToString(string format) {
            return lat.ToDouble().ToString(format) + ", "
                + lon.ToDouble().ToString(format);
        }

        public string ToString(string format, IFormatProvider provider) {
            return lat.ToDouble().ToString(format, provider) + ", "
                + lon.ToDouble().ToString(format, provider);
        }

        public string ToString(IFormatProvider provider) {
            return lat.ToDouble().ToString(provider) + ", "
                + lon.ToDouble().ToString(provider);
        }

        public double Latitue {
            get {
                return lat.ToDouble();
            }
        }

        public double Longitude {
            get {
                return lon.ToDouble();
            }
        }

        public static GpsLocation FromBytes(
            byte[] latSignBytes, byte[] latBytes,
            byte[] lonSignBytes, byte[] lonBytes) {
            return new GpsLocation(
                RationalDegrees.FromBytes(latBytes, latSignBytes[0] == South[0] ? -1 : 1),
                RationalDegrees.FromBytes(lonBytes, lonSignBytes[0] == West[0] ? -1 : 1));
        }

        public bool Equals(GpsLocation? other) {
            if (other == null) {
                return false;
            } else {
                return this.lat.Equals(other.lat) &&
                    this.lon.Equals(other.lon);
            }
        }

        public override bool Equals(object? obj) {
            return this.Equals(obj as GpsLocation);
        }

        public override int GetHashCode() {
            return lat.GetHashCode() ^ lon.GetHashCode();
        }
    }
}
