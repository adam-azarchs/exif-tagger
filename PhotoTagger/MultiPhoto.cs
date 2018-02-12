using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;

namespace PhotoTagger {
    public class MultiPhoto : DependencyObject {
        public ReadOnlyObservableCollection<Photo> PhotoSet {
            get {
                return (ReadOnlyObservableCollection<Photo>)GetValue(
                    PhotoSetProperty);
            }
            set {
                SetValue(PhotoSetProperty, value);
            }
        }

        public static readonly DependencyProperty PhotoSetProperty =
            DependencyProperty.Register("PhotoSet",
                typeof(ReadOnlyObservableCollection<Photo>),
                typeof(MultiPhoto),
                new PropertyMetadata(setChanged));

        private static void setChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e) {
            var mp = d as MultiPhoto;
            if (e.OldValue is INotifyCollectionChanged old &&
                old != null) {
                old.CollectionChanged -= mp.photosChanged;
            }
            if (e.NewValue is INotifyCollectionChanged col &&
                col != null) {
                col.CollectionChanged += mp.photosChanged;
            }
            if (e.NewValue is IReadOnlyCollection<Photo> photos) {
                mp.setFields(photos);
            }
        }

        private void photosChanged(object sender,
            NotifyCollectionChangedEventArgs e) {
            if (sender is IReadOnlyCollection<Photo> photos) {
                this.setFields(photos);
            }
        }

        private void setFields(IReadOnlyCollection<Photo> photos) {
            Title = MultiString.FromCollection(
                    photos, p => p.Title);
            Photographer = MultiString.FromCollection(
                photos, p => p.Photographer);
            FileNames = string.Join(",",
                photos.Select(p => p.FileBaseName).OrderBy(s => s));
            Location = MultiGpsLocation.FromCollection(photos,
                g => g.Location);
            Dates = DateTimeRange.FromList(photos.Select(p => p.DateTaken));
        }

        public MultiString Title {
            get {
                return (MultiString)GetValue(TitleProperty);
            }
            set {
                SetValue(TitleProperty, value);
            }
        }
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title),
                typeof(MultiString), typeof(MultiPhoto),
                new PropertyMetadata(changed));

        public MultiString Photographer {
            get {
                return (MultiString)GetValue(PhotographerProperty);
            }
            set {
                SetValue(PhotographerProperty, value);
            }
        }
        public static readonly DependencyProperty PhotographerProperty =
            DependencyProperty.Register(nameof(Photographer),
                typeof(MultiString), typeof(MultiPhoto),
                new PropertyMetadata(changed));

        public string FileNames {
            get {
                return (string)GetValue(FileNamesProperty);
            }
            set {
                SetValue(FileNamesProperty, value);
            }
        }
        public static readonly DependencyProperty FileNamesProperty =
            DependencyProperty.Register(nameof(FileNames),
                typeof(string), typeof(MultiPhoto),
                new PropertyMetadata(changed));

        public MultiGpsLocation Location {
            get {
                return (MultiGpsLocation)GetValue(LocationProperty);
            }
            set {
                SetValue(LocationProperty, value);
            }
        }
        public static readonly DependencyProperty LocationProperty =
            DependencyProperty.Register(nameof(Location),
                typeof(MultiGpsLocation), typeof(MultiPhoto),
                new PropertyMetadata(changed));
        
        public DateTimeRange? Dates {
            get {
                return (DateTimeRange?)GetValue(DatesProperty);
            }
            set {
                SetValue(DatesProperty, value);
            }
        }
        public static readonly DependencyProperty DatesProperty =
            DependencyProperty.Register(nameof(Dates),
                typeof(DateTimeRange?), typeof(MultiPhoto),
                new PropertyMetadata(changed));

        private static void changed(DependencyObject d,
            DependencyPropertyChangedEventArgs e) {
            var self = (MultiPhoto)d;
            if (e.NewValue is MultiString str &&
                str.State == Consistency.Changed) {
                if (e.Property == TitleProperty) {
                    foreach (var p in self.PhotoSet) {
                        p.Title = str.Value;
                    }
                } else if (e.Property == PhotographerProperty) {
                    foreach (var p in self.PhotoSet) {
                        p.Photographer = str.Value;
                    }
                }
            } else if (e.Property == LocationProperty &&
                e.NewValue is MultiGpsLocation loc &&
                loc.State == Consistency.Changed) {
                foreach (var p in self.PhotoSet) {
                    p.Location = loc.Value;
                }
            }
        }
    }
}
