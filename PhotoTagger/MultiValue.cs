using PhotoTagger.Imaging;
using PhotoTagger.Wpf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace PhotoTagger {

    /// <summary>
    /// Represents whether a collection of values are all the same, different,
    /// or have been changed to be all the same.
    /// </summary>
    public enum Consistency {
        Consistent,
        Inconsistent,
        Changed,
    }

    /// <summary>
    /// Represents a string which is backed by many physical strings.
    /// </summary>
    public class MultiString {
        public MultiString(string value, Consistency state) {
            this.value = value;
            this.state = state;
        }

        private readonly string value;
        private readonly Consistency state;

        public string Value => this.value;
        public Consistency State => this.state;

        private static readonly MultiString empty = new MultiString("",
            Consistency.Consistent);

        public static MultiString FromCollection<T>(
            IReadOnlyCollection<T> items,
            Func<T, string> selector) {
            if (items.Count == 0) {
                return empty;
            }
            string value = null;
            foreach (string item in items.Select(selector)) {
                if (value == null) {
                    if (string.IsNullOrWhiteSpace(item)) {
                        value = "";
                    } else {
                        value = item;
                    }
                } else if (value != item &&
                    (!string.IsNullOrWhiteSpace(item) ||
                     !string.IsNullOrWhiteSpace(value))) {
                    return new MultiString(value, Consistency.Inconsistent);
                }
            }
            return new MultiString(value, Consistency.Consistent);
        }
    }

    public class MultiGpsLocation {
        public MultiGpsLocation(GpsLocation value, Consistency state) {
            this.value = value;
            this.state = state;
        }

        private readonly GpsLocation value;
        private readonly Consistency state;

        public GpsLocation Value => this.value;
        public Consistency State => this.state;

        public static MultiGpsLocation FromCollection<T>(
            IReadOnlyCollection<T> items,
            Func<T, GpsLocation> selector) {
            if (items.Count == 0) {
                return null;
            }
            bool first = true;
            GpsLocation value = null;
            foreach (GpsLocation item in items.Select(selector)) {
                if (first) {
                    value = item;
                    first = false;
                } else if (
                    (item == null && value != null) ||
                    (item != null && !item.Equals(value))) {
                    return new MultiGpsLocation(value, Consistency.Inconsistent);
                }
            }
            return new MultiGpsLocation(value, Consistency.Consistent);
        }
    }

    [ValueConversion(typeof(Consistency), typeof(Brush))]
    [ValueConversion(typeof(MultiString), typeof(Brush))]
    [ValueConversion(typeof(MultiString), typeof(string))]
    [ValueConversion(typeof(string), typeof(MultiString))]
    [ValueConversion(typeof(MultiGpsLocation), typeof(Brush))]
    [ValueConversion(typeof(MultiGpsLocation), typeof(GpsLocation))]
    [ValueConversion(typeof(MultiGpsLocation), typeof(string))]
    [ValueConversion(typeof(GpsLocation), typeof(MultiGpsLocation))]
    [ValueConversion(typeof(string), typeof(MultiGpsLocation))]
    public class MultiValueConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) {
                if (targetType == typeof(Brush)) {
                    return Brushes.LightGreen;
                } else {
                    return null;
                }
            } else if (value is MultiString ms) {
                if (targetType == typeof(Brush)) {
                    return this.Convert(ms.State, targetType, parameter, culture);
                } else if (targetType == typeof(string)) {
                    return ms.Value;
                } else {
                    throw new NotSupportedException("Unsupported type.");
                }
            } else if (value is MultiGpsLocation loc) {
                if (targetType == typeof(Brush)) {
                    return this.Convert(loc.State, targetType, parameter, culture);
                } else if (targetType == typeof(GpsLocation)) {
                    return loc.Value;
                } else if (targetType == typeof(string)) {
                    return GpsLocationValueConverter.ConvertAny(
                        loc.Value, targetType, parameter, culture);
                } else {
                    throw new NotSupportedException("Unsupported type.");
                }
            } else if (value is Consistency state) {
                switch (state) {
                    case Consistency.Consistent:
                        return Brushes.LightGreen;
                    case Consistency.Inconsistent:
                        return Brushes.LightCoral;
                    case Consistency.Changed:
                        return Brushes.LightCyan;
                    default:
                        throw new NotSupportedException("Unsupported state.");
                }
            } else if (value is string v) {
                if (targetType == typeof(MultiString)) {
                    return new MultiString(v, Consistency.Changed);
                } else if (targetType == typeof(MultiGpsLocation)) {
                    var cv = GpsLocationValueConverter.ConvertAny(
                        v, typeof(GpsLocation), parameter, culture);
                    if (cv is GpsLocation newLoc) {
                        return new MultiGpsLocation(newLoc, Consistency.Changed);
                    } else {
                        return cv;
                    }
                } else {
                    throw new NotSupportedException("Unsupported type.");
                }
            } else if (value is GpsLocation vLoc) {
                return new MultiGpsLocation(vLoc, Consistency.Changed);
            } else {
                throw new NotSupportedException("Unsupported type.");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return this.Convert(value, targetType, parameter, culture);
        }
    }
}
