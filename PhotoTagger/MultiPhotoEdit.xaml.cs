using PhotoTagger.Imaging;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;

namespace PhotoTagger {
    /// <summary>
    /// Interaction logic for MultiPhotoEdit.xaml
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class MultiPhotoEdit : UserControl {
        public MultiPhotoEdit() {
            InitializeComponent();
        }

        public MultiPhoto Photos {
            get;
        } = new MultiPhoto() {
            PhotoSet = new ReadOnlyObservableCollection<Photo>(
                new ObservableCollection<Photo>())
        };

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
            DependencyProperty.Register(nameof(PhotoSet),
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
