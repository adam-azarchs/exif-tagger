using PhotoTagger.Imaging;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace PhotoTagger {
    /// <summary>
    /// Interaction logic for MultiPhotoEdit.xaml
    /// </summary>
    public partial class MultiPhotoEdit : UserControl {
        public MultiPhotoEdit() {
            InitializeComponent();
        }

        private readonly MultiPhoto photos = new MultiPhoto() {
            PhotoSet = new ReadOnlyObservableCollection<Photo>(
                new ObservableCollection<Photo>())
        };

        public MultiPhoto Photos {
            get {
                return this.photos;
            }
        }

        public ReadOnlyObservableCollection<Photo> PhotoSet {
            get {
                return (ReadOnlyObservableCollection<Photo>)GetValue(
                    PhotoSetProperty);
            }
            set {
                SetValue(PhotoSetProperty, value);
            }
        }

        public static readonly DependencyProperty PhotoSetProperty =
            DependencyProperty.Register("PhotoSet",
                typeof(ReadOnlyObservableCollection<Photo>),
                typeof(MultiPhotoEdit),
                new PropertyMetadata(setChanged));

        private static void setChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e) {
            (d as MultiPhotoEdit).Photos.PhotoSet =
                e.NewValue as ReadOnlyObservableCollection<Photo>;
        }
    };

}
