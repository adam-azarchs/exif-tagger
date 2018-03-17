using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoTagger.Imaging {
    public class Photo : DependencyObject, INotifyPropertyChanged, IDisposable {
        public Photo(string f) {
            this.FileName = f;
            this.ShortTitle = f;
        }

        public BitmapImage FullImage {
            get {
                return (BitmapImage)GetValue(FullImageProperty);
            }
            set {
                SetValue(FullImageProperty, value);
            }
        }
        public static readonly DependencyProperty FullImageProperty =
            DependencyProperty.Register(nameof(FullImage),
                typeof(BitmapImage), typeof(Photo));

        public BitmapImage ThumbImage {
            get {
                return (BitmapImage)GetValue(ThumbImageProperty);
            }
            set {
                SetValue(ThumbImageProperty, value);
            }
        }
        public static readonly DependencyProperty ThumbImageProperty =
            DependencyProperty.Register(nameof(ThumbImage),
                typeof(BitmapImage), typeof(Photo));

        public string FileName {
            get; private set;
        }
        public string FileBaseName {
            get {
                return System.IO.Path.GetFileNameWithoutExtension(this.FileName);
            }
        }

        internal class Metadata {
            public string Title;
            public string Author;
            public DateTime? DateTaken;
            public GpsLocation Location;
            public short Orientation;
            public int Width;
            public int Height;
        }

        private Metadata setFrom = null;
        private bool setting = true;

        internal void Set(Metadata from) {
            this.setting = true;
            this.setFrom = from;
            if (from.Title != null) {
                this.Title = from.Title;
            } else {
                this.ShortTitle = this.FileBaseName;
            }
            if (from.Author != null) {
                this.Photographer = from.Author;
            }
            if (from.DateTaken.HasValue) {
                this.DateTaken = from.DateTaken.Value;
            }
            if (from.Location != null) {
                this.Location = from.Location;
            }
            this.setting = false;
            changed(this, default);
        }

        public int? Width {
            get {
                return this.setFrom?.Width;
            }
        }

        public int? Height {
            get {
                return this.setFrom?.Height;
            }
        }

        public string Title {
            get {
                return (string)GetValue(TitleProperty);
            }
            set {
                SetValue(TitleProperty, value);
            }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title),
                typeof(string), typeof(Photo), new PropertyMetadata(changed));


        public bool MarkedForDeletion {
            get {
                return (bool)GetValue(MarkedForDeletionProperty);
            }
            set {
                SetValue(MarkedForDeletionProperty, value);
            }
        }

        public static readonly DependencyProperty MarkedForDeletionProperty =
            DependencyProperty.Register(nameof(MarkedForDeletion), typeof(bool),
                typeof(Photo),
                new PropertyMetadata(false));


        public string ShortTitle {
            get {
                return (string)GetValue(ShortTitleProperty);
            }
            private set {
                SetValue(ShortTitleProperty, value);
            }
        }
        public static readonly DependencyProperty ShortTitleProperty =
            DependencyProperty.Register(nameof(ShortTitle),
                typeof(string), typeof(Photo));

        public string Photographer {
            get {
                return (string)GetValue(PhotographerProperty);
            }
            set {
                SetValue(PhotographerProperty, value);
            }
        }
        public static readonly DependencyProperty PhotographerProperty =
            DependencyProperty.Register(nameof(Photographer),
                typeof(string), typeof(Photo), new PropertyMetadata(changed));

        public DateTime? DateTaken {
            get {
                return (DateTime?)GetValue(DateTakenProperty);
            }
            set {
                SetValue(DateTakenProperty, value);
            }
        }
        public static readonly DependencyProperty DateTakenProperty =
            DependencyProperty.Register(nameof(DateTaken),
                typeof(DateTime?), typeof(Photo), new PropertyMetadata(changed));

        public GpsLocation Location {
            get {
                return (GpsLocation)GetValue(LocationProperty);
            }
            set {
                SetValue(LocationProperty, value);
            }
        }
        public static readonly DependencyProperty LocationProperty =
            DependencyProperty.Register(nameof(Location),
                typeof(GpsLocation), typeof(Photo), new PropertyMetadata(changed));

        [Pure]
        private static string firstLine(string text) {
            Contract.Requires(text != null);
            var i = text.IndexOf('\n');
            if (i >= 0) {
                return text.Substring(0, i);
            } else {
                return text;
            }
        }

        private static void changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is Photo photo) {
                if (e.Property == TitleProperty) {
                    if (!string.IsNullOrWhiteSpace(photo.Title)) {
                        photo.ShortTitle = firstLine(photo.Title);
                    } else {
                        photo.ShortTitle = photo.FileBaseName;
                    }
                }
                if (photo.setting) {
                    return;
                }
                bool changed = (photo.Title ?? "") != (photo.setFrom?.Title ?? "") ||
                    (photo.Photographer ?? "") != (photo.setFrom?.Author ?? "") ||
                    photo.DateTaken != photo.setFrom?.DateTaken ||
                    photo.Location != photo.setFrom?.Location;
                if (changed != photo.IsChanged) {
                    photo.IsChanged = changed;
                    if (e.Property?.Name != null) {
                        photo.PropertyChanged?.Invoke(photo, new PropertyChangedEventArgs(e.Property.Name));
                    }
                    photo.PropertyChanged?.Invoke(photo, new PropertyChangedEventArgs(IsChangedProperty.Name));
                }
            }
        }

        public bool IsChanged {
            get {
                return (bool)GetValue(IsChangedProperty);
            }
            set {
                SetValue(IsChangedProperty, value);
            }
        }

        public static readonly DependencyProperty IsChangedProperty =
            DependencyProperty.Register(nameof(IsChanged),
                typeof(bool), typeof(Photo), new PropertyMetadata(false));

        // Memory map of the source image file.
        internal MemoryMappedFile mmap;
        internal UnsafeMemoryMapStream fullImageStream;
        internal readonly SemaphoreSlim loadLock = new SemaphoreSlim(1, 1);

        public event PropertyChangedEventHandler PropertyChanged;

        internal bool Disposed {
            get; private set;
        }

        public void Dispose() {
            this.Disposed = true;
            Thread.MemoryBarrier();
            this.FullImage = null;
            this.ThumbImage = null;
            ThreadPool.QueueUserWorkItem(async _ => {
                bool locked = false;
                try {
                    await this.loadLock.WaitAsync();
                    locked = true;
                    var fullImageStream = Interlocked.Exchange(
                        ref this.fullImageStream, null);
                    if (fullImageStream != null) {
                        fullImageStream.Dispose();
                    }
                    var mmap = Interlocked.Exchange(
                        ref this.mmap, null);
                    if (mmap != null) {
                        mmap.Dispose();
                    }
                } finally {
                    if (locked) {
                        this.loadLock.Release();
                    }
                }
                this.loadLock.Dispose();
            });
        }

        public async Task DisposeNow() {
            this.Disposed = true;
            Thread.MemoryBarrier();
            this.FullImage = null;
            this.ThumbImage = null;
            bool locked = false;
            try {
                await this.loadLock.WaitAsync();
                locked = true;
                var fullImageStream = Interlocked.Exchange(
                    ref this.fullImageStream, null);
                if (fullImageStream != null) {
                    fullImageStream.Dispose();
                }
                var mmap = Interlocked.Exchange(
                    ref this.mmap, null);
                if (mmap != null) {
                    mmap.Dispose();
                }
            } finally {
                if (locked) {
                    this.loadLock.Release();
                }
            }
            this.loadLock.Dispose();
        }

        public async Task Commit(string destination = null) {
            if (this.IsChanged || destination != null) {
                bool locked = false;
                try {
                    await this.loadLock.WaitAsync();
                    locked = true;
                    await ImageLoadManager.Commit(this, destination);
                } finally {
                    if (locked) {
                        this.loadLock.Release();
                    }
                }
            }
        }
    }
}
