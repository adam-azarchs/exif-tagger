using PhotoTagger.Imaging;
using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace PhotoTagger.Wpf {

    [ValueConversion(typeof(GpsLocation), typeof(string))]
    [ValueConversion(typeof(string), typeof(GpsLocation))]
    public class GpsLocationValueConverter : ValidationRule, IValueConverter {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return GpsLocationValueConverter.ConvertAny(value, targetType, parameter, culture);
        }

        public static object? ConvertAny(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is null) {
                return null;
            }
            if (value is string str) {
                if (targetType.IsAssignableFrom(typeof(GpsLocation))) {
                    if (culture == null) {
                        if (!GpsLocation.TryParse(str, out GpsLocation? res)) {
                            return Binding.DoNothing;
                        } else {
                            return res;
                        }
                    } else {
                        if (!GpsLocation.TryParse(str, culture, out GpsLocation? res)) {
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

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return this.Convert(value, targetType, parameter, culture);
        }

        public override ValidationResult Validate(object value, CultureInfo culture) {
            if (value is string str) {
                if (culture == null) {
                    if (GpsLocation.TryParse(str, out GpsLocation? loc) && loc != null) {
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
                    if (GpsLocation.TryParse(str, culture, out _)) {
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
