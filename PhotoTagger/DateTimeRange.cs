using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PhotoTagger {
    public struct DateTimeRange : IEquatable<DateTimeRange> {
        public DateTimeRange(DateTime low, DateTime high) {
            this.low = low;
            this.high = high;
        }

        public DateTimeRange(IEnumerable<DateTime> times) {
            low = high = DateTime.Now;
            bool first = true;
            foreach (var time in times) {
                if (first) {
                    low = time;
                    high = time;
                } else {
                    if (low > time) {
                        low = time;
                    } else if (high < time) {
                        high = time;
                    }
                }
            }
        }

        private readonly DateTime low, high;

        public DateTime Min => low;
        public DateTime Max => high;

        public static DateTimeRange? FromList(IEnumerable<DateTime?> dates) {
            DateTime? high = null;
            DateTime? low = null;
            foreach (var date in dates) {
                if (date.HasValue) {
                    if (!low.HasValue || low.Value > date) {
                        low = date;
                    }
                    if (!high.HasValue || high.Value < date) {
                        high = date;
                    }
                }
            }
            if (low.HasValue) {
                return new DateTimeRange(low.Value, high.Value);
            } else {
                return null;
            }
        }

        public bool IsRange => !Min.Equals(Max);

        #region Equals
        public bool Equals(DateTimeRange other) {
            return this.low.Equals(other.low) &&
                this.high.Equals(other.high);
        }

        public override bool Equals(object obj) {
            if (obj is DateTimeRange dtr) {
                return this.Equals(dtr);
            } else if (obj is DateTime dt) {
                return low.Equals(dt) && high.Equals(dt);
            } else {
                return false;
            }
        }

        public override int GetHashCode() {
            return low.GetHashCode();
        }
        #endregion

        #region ToString
        public override string ToString() {
            if (low.Equals(high)) {
                return low.ToString();
            } else {
                return $"{low.ToString()} to {high.ToString()}";
            }
        }

        public string ToString(IFormatProvider formatProvider) {
            if (low.Equals(high)) {
                return low.ToString(formatProvider);
            } else {
                return $"{low.ToString(formatProvider)} to {high.ToString(formatProvider)}";
            }
        }

        public string ToString(string format, IFormatProvider formatProvider) {
            if (low.Equals(high)) {
                return low.ToString(format, formatProvider);
            } else {
                return $"{low.ToString(format, formatProvider)} to {high.ToString(format, formatProvider)}";
            }
        }
        #endregion
    }

    [ValueConversion(typeof(DateTimeRange?), typeof(Visibility))]
    public class DateTimeRangeIsRangeToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture) {
            if (value is DateTimeRange dtr) {
                return dtr.IsRange ? Visibility.Visible : Visibility.Hidden;
            } else {
                return Visibility.Hidden;
            }
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}