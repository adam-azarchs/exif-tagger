using Microsoft.Win32;
using PhotoCull.Properties;
using PhotoTagger.Imaging;
using PhotoTagger.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PhotoCull {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [SupportedOSPlatform("windows")]
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
            try {
                if ((dialog.ShowDialog(this) ?? false) && dialog.FileNames.Length > 0) {
                    var names = dialog.FileNames;
                    using var refresh = this.photoList.GroupedPhotos.RefreshWhenDone();
                    if (names.Length == 1 && names[0].EndsWith(".txt")) {
                        string[] lines;
                        try {
                            lines = File.ReadAllLines(names[0])
                                .Select(line => line.Trim())
                                .Where(line => line.Length > 0 &&
                                       (line.StartsWith("# Group") ||
                                        !line.StartsWith("#")))
                                .ToArray();
                        } catch (Exception ex) {
                            MessageBox.Show(this,
                                $"Error reading image list {names[0]}: \n{ex}");
                            return;
                        }
                        int start = 0;
                        PhotoGroup? group = null;
                        int order = this.Photos.Count > 0 ? this.Photos.Max(p => p.Group.Order) : 0;
                        for (int i = 0; i < lines.Length; i++) {
                            var line = lines[i];
                            if (line.StartsWith("#")) {
                                this.loadedGroupedList = true;
                                if (start < i) {
                                    if (i == start + 1) {
                                        if (group == null) {
                                            group = new PhotoGroup {
                                                Order = order
                                            };
                                        }
                                        group.Order += lines.Length;
                                    }
                                    addImages(
                                        new ArraySegment<string>(lines, start, i - start),
                                        group);
                                    group = new PhotoGroup {
                                        Order = ++order
                                    };
                                }
                                start = i + 1;
                            }
                        }
                        if (start < lines.Length) {
                            addImages(
                                new ArraySegment<string>(lines, start, lines.Length - start),
                                group);
                        }
                        if (group != null) {
                            SortByGroup(Photos);
                        }
                    } else {
                        addImages(names);
                    }
                }
            } finally {
                if (sender is Control c) {
                    c.IsEnabled = true;
                }
            }
        }

        private readonly ImageLoadManager loader = new() {
            ThumbnailHeight = Settings.Default.ThumbnailHeight
        };

        private bool loadedGroupedList;

        public double ThumbnailHeight {
            get {
                return loader.ThumbnailHeight;
            }
        }

        private void addImages(IEnumerable<string> photos, PhotoGroup? group = null) {
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
            foreach (var filename in photos) {
                if (!photoSet.Contains(filename)) {
                    continue;
                }
                var photo = new Photo(filename);
                loader.EnqueueLoad(photo, this.Photos);
                if (firstMarked >= 0) {
                    this.Photos.Insert(firstMarked++, photo);
                } else {
                    this.Photos.Add(photo);
                }
                if (group != null) {
                    photo.Group = group;
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
            if (this.Photos.Count < 2) {
                this.loadedGroupedList = false;
            }
        }

        public void Dispose() {
            while (this.Photos.Count > 0) {
                int i = this.Photos.Count - 1;
                var p = this.Photos[i];
                this.Photos.RemoveAt(i);
                p.Dispose();
            }
            this.loadedGroupedList = false;
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

        private static string debugName(string fileName) {
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

        private static string debugDataName(string fileName) {
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
            Task? logTask = null;
            if (debugging()) {
                logTask = logReject(good, reject);
            }
            using var refresh = this.photoList.GroupedPhotos.RefreshWhenDone();
            photos.Move(photos.IndexOf(reject), photos.Count - 1);
            reject.MarkedForDeletion = true;
            good.MarkedForDeletion = false;
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
            if (logTask != null) {
                await logTask;
            }
            this.photoList.SelectedValue = null;
            if (photos.Count > 2) {
                reject.Uncache();
            }
            this.deleteButton.IsEnabled = true;
            this.prefetch();
        }

        private static async Task logReject(Photo good, Photo reject) {
            Directory.CreateDirectory(Settings.Default.DebugDest);
            var rname = debugName(reject.FileName);
            var gname = debugName(good.FileName);
            var tasks = new List<Task>(3);
            if (!File.Exists(gname)) {
                tasks.Add(good.Commit(destination: gname));
            }
            if (!File.Exists(rname)) {
                tasks.Add(reject.Commit(destination: rname));
            }
            tasks.Add(File.AppendAllTextAsync(debugDataName(reject.FileName),
                $"compared {{\n" +
                $"  better: \"{Path.GetFileName(gname)}\"\n" +
                $"  worse: \"{Path.GetFileName(rname)}\"\n" +
                $"}}\n"));
            await Task.WhenAll(tasks);
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
            Task? logTask = null;
            if (debugging()) {
                logTask = logDistinct(keep, move);
            }
            using var refresh = this.photoList.GroupedPhotos.RefreshWhenDone();
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
            if (logTask != null) {
                await logTask;
            }
            this.prefetch();
        }

        private async Task logDistinct(Photo keep, Photo move) {
            var kname = debugName(keep.FileName);
            var nname = debugName(move.FileName);
            var tasks = new List<Task>(3);
            if (!File.Exists(kname)) {
                tasks.Add(keep.Commit(destination: kname));
            }
            if (!File.Exists(nname)) {
                tasks.Add(move.Commit(destination: nname));
            }
            if (this.loadedGroupedList) {
                tasks.Add(File.AppendAllTextAsync(debugDataName(move.FileName),
                    $"distinct {{\n" +
                    $"  image: \"{Path.GetFileName(kname)}\"\n" +
                    $"  image: \"{Path.GetFileName(nname)}\"\n" +
                    $"  kind: DISIMILAR\n" +
                    $"}}\n"));
            } else {
                tasks.Add(File.AppendAllTextAsync(debugDataName(move.FileName),
                    $"distinct {{\n" +
                    $"  image: \"{Path.GetFileName(kname)}\"\n" +
                    $"  image: \"{Path.GetFileName(nname)}\"\n" +
                    $"}}\n"));
            }
            await Task.WhenAll(tasks);
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
            var groups = new SortedList<ValueTuple<int, PhotoGroup>, List<int>>();
            for (int i = 0; i < photos.Count; ++i) {
                var p = photos[i];
                var g = new ValueTuple<int, PhotoGroup>(p.MarkedForDeletion ? 2 : 0, p.Group);
                if (!groups.TryGetValue(g, out var items)) {
                    items = new List<int>(photos.Count - i);
                    groups[g] = items;
                }
                items.Add(i);
            }
            var newPhotos = new List<int>(photos.Count);
            var singularPhotos = new List<int>(photos.Count);
            var delPhotos = new List<int>(photos.Count);
            var singularDelPhotos = new List<int>(photos.Count);
            foreach (var group in groups) {
                var list = (group.Value.Count > 1) ? (group.Key.Item1 == 0 ? newPhotos : delPhotos) : (group.Key.Item1 == 0 ? singularPhotos : singularDelPhotos);
                list.AddRange(group.Value);
            }
            newPhotos.AddRange(singularPhotos);
            newPhotos.AddRange(delPhotos);
            newPhotos.AddRange(singularDelPhotos);
            for (int i = 0; i < newPhotos.Count; ++i) {
                if (newPhotos[i] != i) {
                    Contract.Assert(newPhotos[i] > i);
                    photos.Move(newPhotos[i], i);
                    // This invalidates the source indicies for subsequent photos.
                    for (int j = i + 1; j < newPhotos.Count; ++j) {
                        if (newPhotos[j] < newPhotos[i]) {
                            ++newPhotos[j];
                        }
                    }
                }
            }
        }

        private static bool debugging() {
            return !string.IsNullOrWhiteSpace(Settings.Default.DebugDest);
        }

        private void onFilesDrop(object sender, DragEventArgs e) {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files ||
                files.Length == 0) {
                return;
            }
            using (this.photoList.GroupedPhotos.RefreshWhenDone()) {
                addImages(files);
            }
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

        private async void onDragDropMove(object sender, PhotoList.DragDropPhotoEventArgs e) {
            var tasks = new List<Task>(3);
            if (debugging()) {
                var item = e.Item;
                var target = e.Target;
                var iname = debugName(item.FileName);
                var tname = debugName(target.FileName);
                if (!File.Exists(iname)) {
                    tasks.Add(item.Commit(destination: iname));
                }
                if (!File.Exists(tname)) {
                    tasks.Add(target.Commit(destination: tname));
                }
                tasks.Add(File.AppendAllTextAsync(debugDataName(target.FileName),
                        $"distinct {{\n" +
                        $"  image: \"{Path.GetFileName(iname)}\"\n" +
                        $"  image: \"{Path.GetFileName(tname)}\"\n" +
                        $"  kind: SIMILAR\n" +
                        $"}}\n"));
            }
            this.photoList.GroupedPhotos.RefreshNow();
            await Task.WhenAll(tasks);
        }
    }
}
