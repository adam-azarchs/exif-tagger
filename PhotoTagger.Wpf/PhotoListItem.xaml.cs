using PhotoTagger.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

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


        private Point dragStart;

        private void onMouseDown(object sender, MouseButtonEventArgs e) {
            this.dragStart = e.GetPosition(this);
        }

        private void onMouseMove(object sender, MouseEventArgs e) {
            e.Handled = false;
            if (e.LeftButton == MouseButtonState.Pressed && e.RightButton == MouseButtonState.Released) {
                var diff = dragStart - e.GetPosition(this);
                if (diff.Y < SystemParameters.MinimumVerticalDragDistance &&
                    -diff.Y < SystemParameters.MinimumVerticalDragDistance) {
                    return;
                }

                var data = new DataObject(typeof(Photo), this.Photo);
                BitmapImage? img = this.Photo.FullImageIfLoaded;
                if (img != null) {
                    data.SetImage(img);
                }
                DragDrop.DoDragDrop(this,
                    data,
                    DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
                e.Handled = true;
            }
        }
    }
}
