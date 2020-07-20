using PhotoTagger.Imaging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.Contracts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
                var item = findAncestor(e.OriginalSource as DependencyObject);
                if (item == null) {
                    return;
                }
                DragDrop.DoDragDrop(item,
                    new DataObject(typeof(PhotoListItem), item),
                    DragDropEffects.Move);
                e.Handled = true;
            }
        }

        private void onDrop(object sender, DragEventArgs e) {
            e.Handled = false;
            if (e.Data.GetData(typeof(PhotoListItem)) is PhotoListItem item) {
                var target = findAncestor(e.OriginalSource as DependencyObject);
                if (target == null) {
                    return;
                }
                e.Handled = true;
                if (target.Photo.Group != item.Photo.Group) {
                    item.Photo.Group = target.Photo.Group;
                }
                var photos = this.Photos;
                var srcIndex = photos.IndexOf(item.Photo);
                var dstIndex = photos.IndexOf(target.Photo);
                if (srcIndex > dstIndex+1) {
                    photos.Move(srcIndex, dstIndex + 1);
                } else if (dstIndex > srcIndex + 1) {
                    photos.Move(srcIndex, dstIndex);
                }
            }
        }

        private static PhotoListItem? findAncestor(DependencyObject? current) {
            if (current == null) {
                return null;
            }
            if (current is PhotoListItem c) {
                return c;
            }
            return findAncestor(VisualTreeHelper.GetParent(current));
        }
    }
}
