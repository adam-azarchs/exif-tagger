using Microsoft.Win32;
using PhotoTagger.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PhotoTagger {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class TaggerWindow : Window, IDisposable {
        public TaggerWindow() {
            InitializeComponent();

            Photos.CollectionChanged += photoCollectionChanged;
        }

        void photoCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Move) {
                return;
            }
            if (e.OldItems != null) {
                foreach (var item in e.OldItems) {
                    if (item is Photo photo) {
                        photo.PropertyChanged -= photoChanged;
                    }
                }
            }
            if (e.NewItems != null) {
                foreach (var item in e.NewItems) {
                    if (item is Photo photo) {
                        photo.PropertyChanged += photoChanged;
                    }
                }
                foreach (var item in e.NewItems.OfType<Photo>().Take(3)) {
                    item.Prefetch();
                }
            }
            if (e.OldItems != null) {
                // New items are always unchanged to begin.
                this.commitButton.IsEnabled = this.Photos.Any(p => p.IsChanged);
            }
        }

        private void onSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var photos = this.Photos;
            foreach (var photo in e.AddedItems.OfType<Photo>().Take(3)) {
                photo.Prefetch();
                var i = photos.IndexOf(photo);
                if (i >= 0 && i < photos.Count - 2) {
                    photos[i + 1].Prefetch();
                }
            }
        }

        void photoChanged(object sender, PropertyChangedEventArgs e) {
            this.commitButton.IsEnabled = this.Photos.Any(p => p.IsChanged);
        }

        public const int ThumbSize = 48;

        private void addImagesEvent(object sender, RoutedEventArgs e) {
            if (sender is Control b) {
                b.IsEnabled = false;
            }
            const string jpegExtensions = "*.jpg;*.jpeg;*.JPG;*.JPEG";
            const string rawExtensions = "*.dng;*.DNG;" +
                "*.crw;*.CR2;*.MRW;*.3fr;*.ari;*.arw;*.srf;*.sr2;*.bay;*.cri;" +
                "*.cap;*.iiq;*.eip;*.erf;*.fff;*.mef;*.mdc;*.mos;*.nef;*.nrw;" +
                "*.dcs;*.dcr;*.drf;*.k25;*.kdc;*.orf;*.pef;*.ptx;*.pxn;*.R3D;" +
                "*.raf;*.raw;*.rw2;*.rwl;*.rwz;*.srw;*.x3f";
            const string tiffExtensions = "*.tif;*.tiff";
            OpenFileDialog dialog = new OpenFileDialog {
                CheckFileExists = true,
                Filter =
                    "Jpeg images|" + jpegExtensions +
                    "|RAW images|" + rawExtensions +
                    "|TIFF images|" + tiffExtensions +
                    "|All images|" + jpegExtensions + ";" + rawExtensions + ";" + tiffExtensions,
                Multiselect = true,
                Title = "Choose images to load...",
                ShowReadOnly = false,
                ValidateNames = true
            };
            if ((dialog.ShowDialog(this) ?? false) && dialog.FileNames.Length > 0) {
                addImages(dialog.FileNames);
            }
            if (sender is Control c) {
                c.IsEnabled = true;
            }
        }

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
                typeof(ObservableCollection<Photo>), typeof(TaggerWindow),
                new PropertyMetadata() {
                    DefaultValue = new ObservableCollection<Photo>()
                });

        public ObservableCollection<Photo> SelectedPhotos {
            get {
                return this.photoList.Selected;
            }
        }

        private readonly ImageLoadManager loader = new ImageLoadManager();

        private void addImages(string[] photos) {
            var photoSet = new HashSet<string>(photos);
            foreach (var p in this.Photos) {
                photoSet.Remove(p.FileName);
            }
            foreach (var filename in photoSet) {
                var photo = new Photo(filename);
                loader.EnqueueLoad(photo, this.Photos);
                this.Photos.Add(photo);
            }
        }

        private void closeEvent(object sender, RoutedEventArgs e) {
            this.Dispose();
        }

        private void closeSelectedEvent(object sender, RoutedEventArgs e) {
            while (this.SelectedPhotos.Count > 0) {
                int i = this.SelectedPhotos.Count - 1;
                var p = this.SelectedPhotos[i];
                this.Photos.Remove(p);
                p.Dispose();
            }
        }

        public void Dispose() {
            while (this.Photos.Count > 0) {
                int i = this.Photos.Count - 1;
                var p = this.Photos[i];
                this.Photos.RemoveAt(i);
                p.Dispose();
            }
        }

        private void commitEvent(object sender, RoutedEventArgs e) {
            this.commitAll();
        }

        private async void commitAll() {
            try {
                await Task.WhenAll(this.Photos.Select(p => p.Commit()).ToArray());
            } catch (Exception ex) {
                await this.Dispatcher.InvokeAsync(() => {
                    MessageBox.Show(
                        $"Error committing metadata edits: {ex.Message}",
                        "Save Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }

        private void onFilesDrop(object sender, DragEventArgs e) {
            if (!(e.Data.GetData(DataFormats.FileDrop) is string[] files) ||
                files.Length == 0) {
                return;
            }
            addImages(files);
        }

        private void sortImagesEvent(object sender, RoutedEventArgs e) {
            Photos.CollectionChanged -= photoCollectionChanged;
            Photos = new ObservableCollection<Photo>(Photos.OrderBy(
                p => p.DateTaken ?? DateTime.MaxValue));
            Photos.CollectionChanged += photoCollectionChanged;
        }
    }
}
