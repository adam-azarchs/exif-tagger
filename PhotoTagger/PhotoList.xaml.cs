﻿using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace PhotoTagger {
    /// <summary>
    /// Interaction logic for PhotoList.xaml
    /// </summary>
    public partial class PhotoList : UserControl {
        public PhotoList() {
            InitializeComponent();
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
            DependencyProperty.Register("Photos",
                typeof(ObservableCollection<Photo>), typeof(PhotoList));


        private readonly ObservableCollection<Photo> selected = new ObservableCollection<Photo>();

        public ReadOnlyObservableCollection<Photo> Selected {
            get {
                return new ReadOnlyObservableCollection<Photo>(selected);
            }
        }

        private void onSelectionChanged(object sender, SelectionChangedEventArgs e) {
            foreach (var item in e.RemovedItems) {
                this.selected.Remove(item as Photo);
            }
            foreach (var item in e.AddedItems) {
                this.selected.Add(item as Photo);
            }
        }
    }
}
