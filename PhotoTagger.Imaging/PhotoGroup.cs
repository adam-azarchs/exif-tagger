using System.ComponentModel;
using System.Globalization;

namespace PhotoTagger.Imaging {
    public sealed class PhotoGroup : INotifyPropertyChanged {
        public int Order {
            get => order;
            set {
                order = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Order)));
            }
        }

        public override string ToString() {
            return Order.ToString("D8", CultureInfo.InvariantCulture);
        }

        private int order;

        public event PropertyChangedEventHandler? PropertyChanged;

        public static readonly PhotoGroup Default = new PhotoGroup();
    }
}
