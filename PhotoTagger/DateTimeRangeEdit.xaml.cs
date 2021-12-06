using PhotoTagger.Imaging;
using PhotoTagger.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;

namespace PhotoTagger {
    /// <summary>
    /// Interaction logic for DateTimeRangeEdit.xaml
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class DateTimeRangeEdit : UserControl {
        public DateTimeRangeEdit() {
            if (PhotoSet is INotifyCollectionChanged oc) {
                oc.CollectionChanged += setChanged;
            }
            InitializeComponent();
        }

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
            DependencyProperty.Register(nameof(PhotoSet),
                typeof(ReadOnlyObservableCollection<Photo>),
                typeof(DateTimeRangeEdit),
                new PropertyMetadata(setChanged));

        private static void setChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e) {
            var photos = e.NewValue as ReadOnlyObservableCollection<Photo>;
            DateTimeRangeEdit self = (d as DateTimeRangeEdit);
            self.DateRange = DateTimeRange.FromList(
                photos.Select(p => p.DateTaken));
            if (e.OldValue is INotifyCollectionChanged oc) {
                oc.CollectionChanged -= self.setChanged;
            }
            if (photos is INotifyCollectionChanged nc) {
                nc.CollectionChanged += self.setChanged;
            }
        }

        private void setChanged(object sender, NotifyCollectionChangedEventArgs e) {
            DateRange = DateTimeRange.FromList(
                PhotoSet.Select(p => p.DateTaken));
        }


        public DateTimeRange? DateRange {
            get {
                return (DateTimeRange?)GetValue(DateRangeProperty);
            }
            set {
                SetValue(DateRangeProperty, value);
            }
        }

        public static readonly DependencyProperty DateRangeProperty =
            DependencyProperty.Register(nameof(DateRange), typeof(DateTimeRange?),
                typeof(DateTimeRangeEdit),
                new PropertyMetadata(dateChanged));

        private static void dateChanged(DependencyObject d,
                                        DependencyPropertyChangedEventArgs e) {
            var newTime = e.NewValue as DateTimeRange?;
            if (!newTime.HasValue) {
                return;
            }
            if (d is DateTimeRangeEdit self) {
                if (self.PhotoSet.Count == 0) {
                    return;
                }
                DateTimeRange? oldRange = DateTimeRange.FromList(
                    self.PhotoSet.Select(p => p.DateTaken));
                if (!oldRange.HasValue ||
                    !oldRange.Value.IsRange) {
                    self.setAll(newTime.Value.Min);
                } else {
                    var shiftAmount = newTime.Value.Min - oldRange.Value.Min;
                    self.shiftDates(shiftAmount);
                }
            }
        }

        private async void shiftDates(TimeSpan shiftAmount) {
            if (shiftAmount == TimeSpan.Zero) {
                return;
            }
            var part = this.minDatePicker.CurrentDateTimePart;
            foreach (Photo p in this.PhotoSet) {
                if (p.DateTaken.HasValue) {
                    p.DateTaken = p.DateTaken.Value + shiftAmount;
                }
            }
            this.DateRange = DateTimeRange.FromList(
                this.PhotoSet.Select(p => p.DateTaken));
            // restore the CurrentDateTimePart, but only after all of the data
            // binding flow-through has had a chance to propagate.
            await this.Dispatcher.InvokeAsync(() =>
                this.minDatePicker.CurrentDateTimePart = part);
        }

        private void setAllEqual(object sender, RoutedEventArgs e) {
            if (!this.DateRange.HasValue) {
                return;
            }
            var newTime = this.DateRange.Value.Min;
            setAll(newTime);
        }

        private async void setAll(DateTime newTime) {
            bool anyChanged = false;
            var part = this.minDatePicker.CurrentDateTimePart;
            foreach (Photo p in this.PhotoSet) {
                if (p.DateTaken.HasValue &&
                    p.DateTaken.Value != newTime) {
                    p.DateTaken = newTime;
                    anyChanged = true;
                }
            }
            if (anyChanged) {
                this.DateRange = DateTimeRange.FromList(
                    this.PhotoSet.Select(p => p.DateTaken));
                // restore the CurrentDateTimePart, but only after all of the data
                // binding flow-through has had a chance to propagate.
                await this.Dispatcher.InvokeAsync(() =>
                    this.minDatePicker.CurrentDateTimePart = part);
            }
        }
    }
}
