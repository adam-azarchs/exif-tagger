using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace PhotoTagger.Imaging {
    /// <summary>
    /// Grouping and sorting logic for a collection of photos.
    /// </summary>
    public class GroupedPhotosView : ListCollectionView {
        private class GroupedPhotosDescription : GroupDescription {
            public GroupedPhotosDescription(ObservableCollection<Photo> source) {
                this.ItemsSource = source;
                this.CustomSort = new sorter(this.ItemsSource);
            }

            public ObservableCollection<Photo> ItemsSource { get; }

            public override object GroupNameFromItem(object item, int level, CultureInfo culture) {
                if (item is Photo p) {
                    return MakeGroup(p);
                } else {
                    throw new NotSupportedException("Invalid object type " + item.GetType().ToString());
                }
            }

            public static (int, PhotoGroup) MakeGroup(Photo p) {
                return new ValueTuple<int, PhotoGroup>(p.MarkedForDeletion ? 1 : 0, p.Group);
            }

            public override bool NamesMatch(object groupName, object itemName) {
                if (groupName is ValueTuple<int, PhotoGroup> g) {
                    if (itemName is Photo p) {
                        return (g.Item1 == 1) == p.MarkedForDeletion && g.Item2 == p.Group;
                    } else if (itemName is ValueTuple<int, PhotoGroup> i) {
                        return g.Equals(i);
                    }
                }
                return base.NamesMatch(groupName, itemName);
            }

            private struct sorter : IComparer, IComparer<Photo>, IComparer<ValueTuple<int, PhotoGroup>> {
                private readonly ObservableCollection<Photo> ItemsSource;

                public sorter(ObservableCollection<Photo> source) {
                    this.ItemsSource = source;
                }

                public int Compare(object? x, object? y) {
                    if (x == null) {
                        if (y == null) {
                            return 0;
                        }
                        return 1;
                    } else if (y == null) {
                        return -1;
                    }
                    if (x is Photo p1 && y is Photo p2) {
                        return this.Compare(p1, p2);
                    } else if (x is IComparable c) {
                        return c.CompareTo(y);
                    } else if (x is CollectionViewGroup xg && y is CollectionViewGroup yg) {
                        return Compare(xg.Name, yg.Name);
                    }
                    throw new NotSupportedException();
                }

                public int Compare(Photo? x, Photo? y) {
                    if (x == null) {
                        return (y == null) ? 0 : 1;
                    } else if (y == null) {
                        return -1;
                    }
                    return ItemsSource.IndexOf(x).CompareTo(ItemsSource.IndexOf(y));
                }

                public int Compare((int, PhotoGroup) x, (int, PhotoGroup) y) {
                    return x.CompareTo(y);
                }
            }
        }

        public GroupedPhotosView(ObservableCollection<Photo> source) : base(source) {
            description = new GroupedPhotosDescription(source);
            this.GroupDescriptions.Add(description);
            this.Comparer = description.CustomSort;
            this.IsLiveGrouping = true;
            this.IsLiveSorting = false;
            this.IsLiveFiltering = false;
            this.ActiveComparer = description.CustomSort;
            description.GroupNames.Add(new ValueTuple<int, PhotoGroup>(0, PhotoGroup.Default));
            description.GroupNames.Add(new ValueTuple<int, PhotoGroup>(1, PhotoGroup.Default));
        }

        public IDisposable RefreshWhenDone() {
            return new deferredRefresh(this);
        }

        private sealed class deferredRefresh : IDisposable {
            private readonly GroupedPhotosView parent;

            public deferredRefresh(GroupedPhotosView parent) {
                this.parent = parent;
            }

            public void Dispose() {
                this.parent.RefreshOrDefer();
            }
        }

        public void RefreshNow() {
            this.RefreshOrDefer();
        }

        private readonly GroupedPhotosDescription description;

        public override bool PassesFilter(object item) {
            return true;
        }

        public override IComparer Comparer { get; }
    }
}
