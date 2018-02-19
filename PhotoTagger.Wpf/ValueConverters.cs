using PhotoTagger.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PhotoTagger.Wpf {
    [ValueConversion(typeof(IReadOnlyList<Photo>), typeof(Visibility))]
    [ValueConversion(typeof(int), typeof(Visibility))]
    public class ElementCountToVisibilityValueConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter,
              CultureInfo culture) {
            if (value is IReadOnlyList<Photo> list) {
                if (parameter is int desiredCount) {
                    if (list.Count == desiredCount) {
                        return Visibility.Visible;
                    }
                } else if (list.Count > 1) {
                    return Visibility.Visible;
                }
            } else if (value is int count) {
                if (parameter is int desiredCount) {
                    if (count == desiredCount) {
                        return Visibility.Visible;
                    }
                } else if (parameter is IConvertible para) {
                    if (para.ToInt32(CultureInfo.InvariantCulture) == count) {
                        return Visibility.Visible;
                    }
                } else if (count > 1) {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }

    [ValueConversion(typeof(IReadOnlyList<Photo>), typeof(bool))]
    [ValueConversion(typeof(int), typeof(bool))]
    public class AnyToEnabledValueConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is IReadOnlyList<Photo> photos) {
                return photos.Count > 0;
            } else if (value is int count) {
                return count > 0;
            } else {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }

    [ValueConversion(typeof(IReadOnlyList<Photo>), typeof(bool))]
    public class AnyChangedToEnabledValueConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is IReadOnlyList<Photo> photos) {
                return photos.Any(p => p.IsChanged);
            } else {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }

    [ValueConversion(typeof(bool), typeof(Brush))]
    public class PhotoHasChangedToBrushValueConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is bool changed) {
                return Colors.DarkRed;
            } else {
                return SystemColors.ControlTextBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is Brush c) {
                return c == SystemColors.ControlTextBrush;
            } else {
                return true;
            }
        }
    }

    [ValueConversion(typeof(IReadOnlyList<Photo>), typeof(DateTimeRange?))]
    public class PhotosToDateRangeValueConverter : IValueConverter {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture) {
            if (value is IReadOnlyList<Photo> photos) {
                return DateTimeRange.FromList(photos.Select(p => p.DateTaken));
            } else {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
