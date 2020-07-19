using PhotoTagger.Imaging;
using System;
using System.Globalization;
using System.Windows.Data;

namespace PhotoTagger.Wpf {
    [ValueConversion(typeof(PhotoGroup), typeof(int))]
    [ValueConversion(typeof(PhotoGroup), typeof(string))]
    public class PhotoGroupValueConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is PhotoGroup g) {
                if (targetType.IsAssignableFrom(typeof(int))) {
                    return g.Order;
                }
                if (targetType.IsAssignableFrom(typeof(string))) {
                    return g.ToString();
                }
                throw new NotSupportedException($"Invalid destination type {targetType}");
            }
            if (value == null) {
                if (targetType.IsAssignableFrom(typeof(int))) {
                    return int.MaxValue;
                }
                if (targetType.IsAssignableFrom(typeof(string))) {
                    return "";
                }
                throw new NotSupportedException($"Invalid destination type {targetType}");
            }
            throw new NotSupportedException($"Invalid source type {value.GetType()}");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
