using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PhotoTagger.Imaging {
    public sealed class PhotoGroup : INotifyPropertyChanged,
        IComparable, IComparable<PhotoGroup> {
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

        public int CompareTo(object? obj) {
            return this.CompareTo(obj as PhotoGroup);
        }

        public int CompareTo(PhotoGroup? other) {
            if (other == null) {
                return -1;
            }
            return order.CompareTo(other.order);
        }

        private int order;

        public event PropertyChangedEventHandler? PropertyChanged;

        public static readonly PhotoGroup Default = new();
    }
}
