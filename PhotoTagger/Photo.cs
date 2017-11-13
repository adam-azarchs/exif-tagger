using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoTagger {
    public class Photo : DependencyObject, IDisposable {
        public Photo(string f) {
            this.FileName = f;
            this.Title = this.FileBaseName;
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
            DependencyProperty.Register("FullImage",
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
            DependencyProperty.Register("ThumbImage",
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

        internal void Set(Metadata from) {
            if (from.Title != null) {
                this.Title = from.Title;
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
            this.IsChanged = false;
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
            DependencyProperty.Register("Title",
                typeof(string), typeof(Photo), new PropertyMetadata(changed));

        public string Photographer {
            get {
                return (string)GetValue(PhotographerProperty);
            }
            set {
                SetValue(PhotographerProperty, value);
            }
        }
        public static readonly DependencyProperty PhotographerProperty =
            DependencyProperty.Register("Photographer",
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
            DependencyProperty.Register("DateTaken",
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
            DependencyProperty.Register("Location",
                typeof(GpsLocation), typeof(Photo), new PropertyMetadata(changed));

        private static void changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is Photo photo) {
                photo.IsChanged = true;
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
            DependencyProperty.Register("IsChanged",
                typeof(bool), typeof(Photo), new PropertyMetadata(false));

        // Memory map of the source image file.
        internal MemoryMappedFile mmap;
        internal UnsafeMemoryMapStream fullImageStream;
        internal readonly SemaphoreSlim loadLock = new SemaphoreSlim(1, 1);
        internal bool Disposed { get; private set; }

        public void Dispose() {
            this.Disposed = true;
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
            });
        }

        public async Task Commit() {
            if (this.IsChanged) {
                bool locked = false;
                try {
                    await this.loadLock.WaitAsync();
                    locked = true;
                    await ImageLoadManager.Commit(this);
                } finally {
                    if (locked) {
                        this.loadLock.Release();
                    }
                }
            }
        }
    }
}
