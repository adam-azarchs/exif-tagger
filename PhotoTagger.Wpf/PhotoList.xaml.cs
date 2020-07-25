using PhotoTagger.Imaging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.Contracts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        public event SelectionChangedEventHandler? OnSelectionChanged;

        private void onSelectionChanged(object sender, SelectionChangedEventArgs e) {
            foreach (var item in e.RemovedItems) {
                Photo? p = item as Photo;
                Contract.Assert(p != null);
                this.Selected.Remove(p);
            }
            foreach (var item in e.AddedItems) {
                Photo? p = item as Photo;
                Contract.Assert(p != null);
                this.Selected.Add(p);
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

        public object? SelectedValue {
            get {
                return GetValue(SelectedValueProperty);
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

        private void onDrop(object sender, DragEventArgs e) {
            e.Handled = false;
            if (e.Data.GetData(typeof(Photo)) is Photo item) {
                var target = findPhoto(e.OriginalSource as DependencyObject);
                if (target == null) {
                    return;
                }
                e.Handled = true;
                if (target == item) {
                    return;
                }
                if (target.Group != item.Group) {
                    item.Group = target.Group;
                }
                var photos = this.Photos;
                var srcIndex = photos.IndexOf(item);
                var dstIndex = photos.IndexOf(target);
                if (srcIndex > dstIndex+1) {
                    photos.Move(srcIndex, dstIndex + 1);
                } else if (dstIndex > srcIndex + 1) {
                    photos.Move(srcIndex, dstIndex);
                }
            }
        }

        private static Photo? findPhoto(DependencyObject? current) {
            if (current == null) {
                return null;
            }
            if (current is PhotoListItem i) {
                return i.Photo;
            } else if (current is Photo p) {
                return p;
            } else if (current is ContentControl c) {
                if (c.Content is Photo cp) {
                    return cp;
                }
            }
            return findPhoto(VisualTreeHelper.GetParent(current));
        }
    }
}
