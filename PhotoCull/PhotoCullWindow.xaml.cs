using Microsoft.Win32;
using PhotoCull.Properties;
using PhotoTagger.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PhotoCull {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class PhotoCullWindow : Window {
        public PhotoCullWindow() {
            InitializeComponent();
            // Disable downsampling since users will be comparing focus and
            // other properties at full-resolution detail levels.
            ImageLoadManager.DownsampleFullImage = false;
        }

        public ObservableCollection<Photo> Photos {
            get {
                return (ObservableCollection<Photo>)GetValue(PhotosProperty);
            }
        }

        public static readonly DependencyProperty PhotosProperty =
                    DependencyProperty.Register(nameof(Photos),
                        typeof(ObservableCollection<Photo>), typeof(PhotoCullWindow),
                        new PropertyMetadata() {
                            DefaultValue = new ObservableCollection<Photo>()
                        });

        public ObservableCollection<Photo> SelectedPhotos {
            get {
                return this.photoList.Selected;
            }
        }

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
                    "|Text files|*.txt" +
                    "|All images|" + jpegExtensions + ";*.txt;" + rawExtensions + ";" + tiffExtensions,
                Multiselect = true,
                Title = "Choose images to load...",
                ShowReadOnly = false,
                ValidateNames = true
            };
            if ((dialog.ShowDialog(this) ?? false) && dialog.FileNames.Length > 0) {
                var names = dialog.FileNames;
                if (names.Length == 1 && names[0].EndsWith(".txt")) {
                    try {
                        names = File.ReadAllLines(names[0])
                            .Select(line => line.Trim())
                            .Where(line => line.Length > 0 && !line.StartsWith("#"))
                            .ToArray();
                    } catch (Exception ex) {
                        MessageBox.Show(this,
                            $"Error reading image list {names[0]}: \n{ex}");
                    }
                }
                addImages(names);
            }
            if (sender is Control c) {
                c.IsEnabled = true;
            }
        }

        private readonly ImageLoadManager loader = new ImageLoadManager() {
            ThumbnailHeight = Settings.Default.ThumbnailHeight
        };

        public double ThumbnailHeight {
            get {
                return loader.ThumbnailHeight;
            }
        }

        private void addImages(string[] photos) {
            var photoSet = new HashSet<string>(photos);
            foreach (var p in this.Photos) {
                photoSet.Remove(p.FileName);
            }
            int firstMarked = -1;
            {
                int i = 0;
                foreach (var photo in Photos) {
                    if (photo.MarkedForDeletion) {
                        firstMarked = i;
                        break;
                    }
                    ++i;
                }
            }
            foreach (var filename in photoSet) {
                var photo = new Photo(filename);
                loader.EnqueueLoad(photo, this.Photos);
                if (firstMarked >= 0) {
                    this.Photos.Insert(firstMarked++, photo);
                } else {
                    this.Photos.Add(photo);
                }
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

        private async void deleteEvent(object sender, RoutedEventArgs e) {
            var rejects = this.Photos.Where(p => p.MarkedForDeletion).ToArray();
            var result = MessageBox.Show(this, string.Join(", ", rejects.Select(p => p.FileName)),
                    "Confirm delete of " + rejects.Length + " files.",
                    MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);
            if (result != MessageBoxResult.OK) {
                return;
            }
            await Task.WhenAll(rejects.Select(async photo => {
                this.Photos.Remove(photo);
                await photo.DisposeNow();
                try {
                    File.Delete(photo.FileName);
                } catch (Exception ex) {
                    MessageBox.Show(this,
                        $"Error deleting {photo.FileName}: \n{ex}");
                }
            }).ToArray());
            this.deleteButton.IsEnabled = false;
        }

        private string debugName(string fileName) {
            string dirname;
            if (Path.IsPathRooted(Settings.Default.DebugDest)) {
                dirname = Settings.Default.DebugDest;
            } else {
                var dir = Path.GetDirectoryName(fileName);
                Contract.Assert(dir != null);
                dirname = Path.Combine(dir,
                    Settings.Default.DebugDest);
            }
            Directory.CreateDirectory(dirname);
            return Path.Combine(dirname,
                Path.GetFileNameWithoutExtension(fileName) + ".jpg");
        }

        private string debugDataName(string fileName) {
            if (Path.IsPathRooted(Settings.Default.DebugDest)) {
                return Path.Combine(Settings.Default.DebugDest,
                    "compare.pbtxt");
            } else {
                var dir = Path.GetDirectoryName(fileName);
                Contract.Assert(dir != null);
                return Path.Combine(dir,
                    Settings.Default.DebugDest,
                    "compare.pbtxt");
            }
        }

        private async void onClickFirst(object sender, RoutedEventArgs e) {
            await reject(false);
        }

        private async void onClickSecond(object sender, RoutedEventArgs e) {
            await reject(true);
        }

        private void prefetch() {
            var photos = this.Photos;
            if (photos.Count > 2) {
                foreach (var p in photos.Take(3)) {
                    p.Prefetch();
                }
            }
        }

        private async Task reject(bool first) {
            var photos = this.Photos;
            if (photos.Count == 0) {
                return;
            }
            if (photos.Count == 1) {
                photos[0].MarkedForDeletion = first;
                this.deleteButton.IsEnabled = first;
                return;
            }
            var good = first ? secondZoom.Photo : firstZoom.Photo;
            var reject = first ? firstZoom.Photo : secondZoom.Photo;
            if (debugging()) {
                Directory.CreateDirectory(Settings.Default.DebugDest);
                var rname = debugName(reject.FileName);
                var gname = debugName(good.FileName);
                if (!File.Exists(gname)) {
                    await good.Commit(destination: gname);
                }
                if (!File.Exists(rname)) {
                    await reject.Commit(destination: rname);
                }
                File.AppendAllText(debugDataName(reject.FileName),
                    $"compared {{\n" +
                    $"  better: \"{Path.GetFileName(gname)}\"\n" +
                    $"  worse: \"{Path.GetFileName(rname)}\"\n" +
                    $"}}\n");
            }
            photos.Move(photos.IndexOf(reject), photos.Count - 1);
            reject.MarkedForDeletion = true;
            good.MarkedForDeletion = false;
            reject.Uncache();
            var goodIndex = photos.IndexOf(good);
            if (photos.Any(p => p != good && p.Group == good.Group && !p.MarkedForDeletion)) {
                if (goodIndex != 0) {
                    photos.Move(goodIndex, 0);
                }
                int minOrder = photos.Min(p => p.Group.Order);
                if (good.Group.Order != minOrder) {
                    good.Group.Order = minOrder - 1;
                }
            } else {
                var destIndex = photos
                    .Select((photo, index) => (photo.MarkedForDeletion, index))
                    .Where(pi => !pi.MarkedForDeletion)
                    .Max(pi => pi.index);
                if (destIndex > goodIndex) {
                    photos.Move(goodIndex, destIndex);
                }
                // Move this group to the end.
                int maxOrder = photos.Max(p => p.Group.Order);
                if (good.Group.Order != maxOrder) {
                    good.Group.Order = maxOrder + 1;
                }
            }
            SortByGroup(photos);
            this.deleteButton.IsEnabled = true;
            this.photoList.SelectedValue = null;
            this.prefetch();
        }

        private async void onDistinctFirst(object sender, RoutedEventArgs e) {
            await distinct(true);
        }

        private async void onDistinctSecond(object sender, RoutedEventArgs e) {
            await distinct(false);
        }

        private async Task distinct(bool moveFirst) {
            ObservableCollection<Photo> photos = this.Photos;
            if (photos.Count < 2) {
                return;
            }
            var keep = moveFirst ? secondZoom.Photo : firstZoom.Photo;
            var move = moveFirst ? firstZoom.Photo : secondZoom.Photo;
            if (debugging()) {
                var kname = debugName(keep.FileName);
                var nname = debugName(move.FileName);
                if (!File.Exists(kname)) {
                    await keep.Commit(destination: kname);
                }
                if (!File.Exists(nname)) {
                    await move.Commit(destination: nname);
                }
                File.AppendAllText(debugDataName(move.FileName),
                    $"distinct {{\n" +
                    $"  image: \"{Path.GetFileName(kname)}\"\n" +
                    $"  image: \"{Path.GetFileName(nname)}\"\n" +
                    $"}}\n");
            }
            keep.MarkedForDeletion = false;
            move.MarkedForDeletion = false;
            // Check if the kept one was actually the last one in the group, in
            // which case we're done with it and want to move it to the end
            // instead.
            if (!photos.Any(p => p != keep && !p.MarkedForDeletion && p.Group == keep.Group) &&
                photos.Any(p => p != move && !p.MarkedForDeletion && p.Group == move.Group)) {
                var t = move;
                move = keep;
                keep = t;
            }
            move.NotGroup.Add(keep.Group);
            if (move.Group == keep.Group) {
                move.Group = getNewGroup(photos, move);
            }
            keep.NotGroup.Add(move.Group);
            if (photos.Count > 2) {
                // Move the kept photo to the beginning.
                var keepIndex = photos.IndexOf(keep);
                bool keepIsntSingleton = photos.Any(p =>
                    p != keep &&
                    !p.MarkedForDeletion &&
                    p.Group == keep.Group);
                if (keepIsntSingleton) {
                    if (keepIndex != 0) {
                        photos.Move(keepIndex, 0);
                    }
                } else {
                    var destIndex = photos
                        .Select((photo, index) => (photo.MarkedForDeletion, index))
                        .Where(pi => !pi.MarkedForDeletion)
                        .Max(pi => pi.index);
                    if (destIndex > keepIndex) {
                        photos.Move(keepIndex, destIndex);
                    }
                }
                // Move the punted photo to the end of the next group.
                var moveIndex = photos.IndexOf(move);
                for (int i = moveIndex + 1; i <= photos.Count; i++) {
                    if (i == photos.Count ||
                        photos[i].MarkedForDeletion ||
                        photos[i].Group.Order > move.Group.Order) {
                        if (i != moveIndex) {
                            photos.Move(moveIndex, i - 1);
                        }
                        break;
                    }
                }
                if (keepIsntSingleton) {
                    // Move to beginning.
                    int minOrder = photos.Min(p => p.Group.Order);
                    if (keep.Group.Order != minOrder) {
                        keep.Group.Order = minOrder - 1;
                    }
                } else {
                    // Move to the end.
                    int maxOrder = photos.Max(p => p.Group.Order);
                    if (keep.Group.Order != maxOrder) {
                        keep.Group.Order = maxOrder + 1;
                    }
                }
                SortByGroup(photos);
            }

            this.deleteButton.IsEnabled = photos.Any(p => p.MarkedForDeletion);
            this.photoList.SelectedValue = null;
            this.prefetch();
        }

        private static PhotoGroup getNewGroup(ObservableCollection<Photo> photos, Photo move) {
            foreach (var p in photos) {
                if (p.Group.Order > move.Group.Order && !move.NotGroup.Contains(p.Group)) {
                    return p.Group;
                }
            }
            return new PhotoGroup() { Order = move.NotGroup.Max(g => g.Order) + 1 };
        }

        public static void SortByGroup(ObservableCollection<Photo> photos) {
            if (photos.Count < 2) {
                return;
            }
            var groupCounts = new Dictionary<PhotoGroup, int>();
            foreach (var p in photos) {
                groupCounts.TryGetValue(p.Group, out int count);
                if (!p.MarkedForDeletion) {
                    ++count;
                }
                groupCounts[p.Group] = count;
            }
            var newPhotos = photos.Select((ph, index) => (ph, index))
                .OrderBy(pi => (pi.ph.MarkedForDeletion ? 2 : 0) + (groupCounts[pi.ph.Group] > 1 ? 0 : 1))
                .ThenBy(pi => pi.ph.Group.Order)
                .ThenBy(pi => pi.index)
                .Select(pi => pi.ph).ToArray();
            for (int i = 0; i < newPhotos.Length; i++) {
                photos[i] = newPhotos[i];
            }
        }

        private static bool debugging() {
            return !string.IsNullOrWhiteSpace(Settings.Default.DebugDest);
        }

        private void onFilesDrop(object sender, DragEventArgs e) {
            if (!(e.Data.GetData(DataFormats.FileDrop) is string[] files) ||
                files.Length == 0) {
                return;
            }
            addImages(files);
        }

        private void onSelectionChanged(object sender, SelectionChangedEventArgs e) {
            while (this.SelectedPhotos.Count > 2) {
                this.SelectedPhotos.RemoveAt(0);
            }
            if (this.SelectedPhotos.Count == 1 &&
                this.SelectedPhotos[0] == this.Photos[1]) {
                // Don't allow selecting the second photo in the list.
                // Otherwise we're just comparing that photo to itself.
                this.SelectedPhotos.RemoveAt(0);
            }
        }
    }
}
