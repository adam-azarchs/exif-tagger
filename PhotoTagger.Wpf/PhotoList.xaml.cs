using PhotoTagger.Imaging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace PhotoTagger.Wpf {
    /// <summary>
    /// Interaction logic for PhotoList.xaml
    /// </summary>
    public partial class PhotoList : UserControl {
        public PhotoList() {
            InitializeComponent();
            Selected.CollectionChanged += onSelectedForcedChange;
        }

        public SelectionMode SelectionMode {
            get {
                return (SelectionMode)GetValue(SelectionModeProperty);
            }
            set {
                SetValue(SelectionModeProperty, value);
            }
        }

        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register(nameof(SelectionMode), typeof(SelectionMode),
                typeof(PhotoList),
                new PropertyMetadata(SelectionMode.Single));


        public ObservableCollection<Photo> Photos {
            get {
                return (ObservableCollection<Photo>)GetValue(PhotosProperty);
            }
            set {
                SetValue(PhotosProperty, value);
            }
        }

        public static readonly DependencyProperty PhotosProperty =
            DependencyProperty.Register(nameof(Photos),
                typeof(ObservableCollection<Photo>), typeof(PhotoList));

        public ObservableCollection<Photo> Selected {
            get;
        } = new ObservableCollection<Photo>();

        public event SelectionChangedEventHandler OnSelectionChanged;

        private void onSelectionChanged(object sender, SelectionChangedEventArgs e) {
            foreach (var item in e.RemovedItems) {
                this.Selected.Remove(item as Photo);
            }
            foreach (var item in e.AddedItems) {
                this.Selected.Add(item as Photo);
            }
            OnSelectionChanged?.Invoke(sender, e);
        }

        private void onSelectedForcedChange(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Remove &&
                this.SelectionMode == SelectionMode.Multiple) {
                foreach (var item in e.OldItems) {
                    this.ListBox.SelectedItems.Remove(item);
                }
            }
            // TODO: support other modification types.
        }

        public object SelectedValue {
            get {
                return (object)GetValue(SelectedValueProperty);
            }
            set {
                SetValue(SelectedValueProperty, value);
            }
        }

        public static readonly DependencyProperty SelectedValueProperty =
            DependencyProperty.Register(nameof(SelectedValue), typeof(object),
                typeof(PhotoList));

        public double ThumbnailHeight {
            get {
                return (double)GetValue(ThumbnailHeightProperty);
            }
            set {
                SetValue(ThumbnailHeightProperty, value);
            }
        }

        public static readonly DependencyProperty ThumbnailHeightProperty =
            DependencyProperty.Register(nameof(ThumbnailHeight), typeof(double),
                typeof(PhotoList),
                new PropertyMetadata(48.0));
    }
}
