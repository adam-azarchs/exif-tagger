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

        public ReadOnlyObservableCollection<Photo> PhotoSet {
            get {
                return (ReadOnlyObservableCollection<Photo>)GetValue(PhotoSetProperty);
            }
            set {
                SetValue(PhotoSetProperty, value);
            }
        }
        public static readonly DependencyProperty PhotoSetProperty =
            DependencyProperty.Register("PhotoSet",
                typeof(ReadOnlyObservableCollection<Photo>),
                typeof(MultiPhotoEdit));
    }
}
