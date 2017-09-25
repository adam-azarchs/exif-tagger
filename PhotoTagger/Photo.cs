using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoTagger {
    public class Photo : DependencyObject {
        public Photo(string f) {
            this.FileName = f;
            this.Title = this.FileBaseName;
        }

        public BitmapImage CurrentDisplayImage {
            get {
                return (BitmapImage)GetValue(CurrentDisplayImageProperty);
            }
            set {
                SetValue(CurrentDisplayImageProperty, value);
            }
        }
        public static readonly DependencyProperty CurrentDisplayImageProperty =
            DependencyProperty.Register("CurrentDisplayImage",
                typeof(BitmapImage), typeof(Photo));

        public string FileName {
            get; private set;
        }
        public string FileBaseName {
            get {
                return System.IO.Path.GetFileNameWithoutExtension(this.FileName);
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
    }
}
