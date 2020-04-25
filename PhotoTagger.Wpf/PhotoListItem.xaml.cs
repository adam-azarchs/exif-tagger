using PhotoTagger.Imaging;
using System.Windows;
using System.Windows.Controls;

namespace PhotoTagger.Wpf {
    /// <summary>
    /// Interaction logic for PhotoListItem.xaml
    /// </summary>
    public partial class PhotoListItem : UserControl {
        public PhotoListItem() {
            InitializeComponent();
        }

        public static readonly DependencyProperty PhotoProperty =
            DependencyProperty.Register(
                "Photo", typeof(Photo), typeof(PhotoListItem));
        public Photo Photo {
            get {
                return (Photo)GetValue(PhotoProperty);
            }
            set {
                SetValue(PhotoProperty, value);
            }
        }
    }
}
