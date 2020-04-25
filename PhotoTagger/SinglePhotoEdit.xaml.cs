using PhotoTagger.Imaging;
using System.Windows;
using System.Windows.Controls;

namespace PhotoTagger {
    /// <summary>
    /// Interaction logic for SinglePhotoEdit.xaml
    /// </summary>
    public partial class SinglePhotoEdit : UserControl {
        public SinglePhotoEdit() {
            InitializeComponent();
        }

        public Photo Photo {
            get {
                return (Photo)GetValue(PhotoProperty);
            }
            set {
                SetValue(PhotoProperty, value);
            }
        }

        public static readonly DependencyProperty PhotoProperty =
            DependencyProperty.Register(nameof(Photo),
                typeof(Photo),
                typeof(SinglePhotoEdit),
                new PropertyMetadata(null));
    }
}
