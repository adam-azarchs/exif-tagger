using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;

namespace PhotoTagger {
    /// <summary>
    /// Stores latitude and longitude information in a form convenient for
    /// loading to saving from or to EXIF metadata.  Immutable once
    /// constructed.
    /// </summary>
    public class GpsLocation {
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

        private static readonly Regex LocRegex = new Regex(
            @"(-?[0-9]+\.?[0-9]*)\s*([nsNS]?)\s*,?\s*(-?[0-9]+\.?[0-9]*)\s*([ewEW]?)",
            RegexOptions.Compiled);

        public GpsLocation(RationalDegrees lat, RationalDegrees lon) {
            this.lat = lat;
            this.lon = lon;
        }

        public static bool TryParse(string value, out GpsLocation result) {
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

        public static bool TryParse(string value, IFormatProvider provider, out GpsLocation result) {
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

        public static GpsLocation FromLongs(
            string latSignBytes, uint[] latBytes,
            string lonSignBytes, uint[] lonBytes) {
            return new GpsLocation(
                RationalDegrees.FromLongs(latBytes, latSignBytes[0] == South[0] ? -1 : 1),
                RationalDegrees.FromLongs(lonBytes, lonSignBytes[0] == West[0] ? -1 : 1));
        }
    }

    [ValueConversion(typeof(GpsLocation), typeof(string))]
    [ValueConversion(typeof(string), typeof(GpsLocation))]
    public class GpsLocationValueConverter : ValidationRule, IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is null) {
                return null;
            }
            if (value is string str) {
                if (targetType.IsAssignableFrom(typeof(GpsLocation))) {
                    if (culture == null) {
                        if (!GpsLocation.TryParse(str, out GpsLocation res)) {
                            return Binding.DoNothing;
                        } else {
                            return res;
                        }
                    } else {
                        if (!GpsLocation.TryParse(str, culture, out GpsLocation res)) {
                            return Binding.DoNothing;
                        } else {
                            return res;
                        }
                    }
                } else {
                    throw new NotSupportedException(string.Format("Invalid target type {0}", targetType));
                }
            } else if (value is GpsLocation loc) {
                if (targetType.IsAssignableFrom(typeof(string))) {
                    if (parameter is string format) {
                        if (culture == null) {
                            return loc.ToString(format);
                        } else {
                            return loc.ToString(format, culture);
                        }
                    } else {
                        if (culture == null) {
                            return loc.ToString();
                        } else {
                            return loc.ToString(culture);
                        }
                    }
                } else {
                    throw new NotSupportedException(string.Format("Invalid target type {0}", targetType));
                }
            } else {
                throw new NotSupportedException(string.Format("Invalid source type {0}", value.GetType()));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return this.Convert(value, targetType, parameter, culture);
        }

        public override ValidationResult Validate(object value, CultureInfo culture) {
            if (value is string str) {
                if (culture == null) {
                    if (GpsLocation.TryParse(str, out GpsLocation loc)) {
                        var lat = loc.Latitue;
                        if (lat > 90 || lat < -90) {
                            return new ValidationResult(false, string.Format("Invalid latitude {0}.", lat));
                        }
                        var lon = loc.Longitude;
                        if (lon > 180 || lon < -180) {
                            return new ValidationResult(false, string.Format("Invalid latitude {0}.", lon));
                        }
                        return new ValidationResult(true, null);
                    } else {
                        return new ValidationResult(false, "Could not parse location string.");
                    }
                } else {
                    if (GpsLocation.TryParse(str, culture, out GpsLocation res)) {
                        return new ValidationResult(true, null);
                    } else {
                        return new ValidationResult(false, "Could not parse location string.");
                    }
                }
            } else if (value is GpsLocation loc) {
                var lat = loc.Latitue;
                if (lat > 90 || lat < -90) {
                    return new ValidationResult(false, string.Format("Invalid latitude {0}.", lat));
                }
                var lon = loc.Longitude;
                if (lon > 180 || lon < -180) {
                    return new ValidationResult(false, string.Format("Invalid latitude {0}.", lon));
                }
                return new ValidationResult(true, null);
            } else {
                return new ValidationResult(false, string.Format("Invalid source type {0}", value.GetType()));
            }
        }
    }
}
