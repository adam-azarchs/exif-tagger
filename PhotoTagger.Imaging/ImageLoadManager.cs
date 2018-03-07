using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoTagger.Imaging {
    public class ImageLoadManager {

        public void EnqueueLoad(Photo photo,
                                ObservableCollection<Photo> list) {
            metadataReads.Add(new Tuple<Photo, ObservableCollection<Photo>>(photo, list));
            makeIOThread();
        }

        private BlockingCollection<Tuple<Photo, ObservableCollection<Photo>>> metadataReads =
            new BlockingCollection<Tuple<Photo, ObservableCollection<Photo>>>();

        private BlockingCollection<Tuple<Photo, Photo.Metadata>> fullsizeReads =
            new BlockingCollection<Tuple<Photo, Photo.Metadata>>();

        private static readonly int MaxIOThreads = Math.Min(Environment.ProcessorCount, 3);
        private int runningIOThreads = 0;

        private void makeIOThread() {
            if (runningIOThreads < MaxIOThreads) {
                ThreadPool.QueueUserWorkItem(ioWorker);
            }
        }

        private async void ioWorker(object state) {
            while (Interlocked.Increment(ref runningIOThreads) > MaxIOThreads) {
                if (Interlocked.Decrement(ref runningIOThreads) >= MaxIOThreads) {
                    return;
                }
            }
            try {
                while (metadataReads.Count > 0 || fullsizeReads.Count > 0) {
                    if (metadataReads.TryTake(out var meta)) {
                        await loadMeta(meta.Item1, meta.Item2);
                    } else if (fullsizeReads.TryTake(out var photo)) {
                        await setImage(photo.Item1, photo.Item2, true);
                    }
                }
            } finally {
                Interlocked.Decrement(ref runningIOThreads);
            }
            if (metadataReads.Count > 0 || fullsizeReads.Count > 0) {
                ThreadPool.QueueUserWorkItem(ioWorker);
            }
        }

        private static string mmapName(string fname) {
            return fname.Replace('\\', '/') + fname.GetHashCode().ToString();
        }

        public int ThumbnailHeight {
            get; set;
        } = 48;

        private async Task loadMeta(Photo photo,
                                    ObservableCollection<Photo> list) {
            try {
                var mmap = MemoryMappedFile.CreateFromFile(
                    File.Open(photo.FileName,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Delete | FileShare.Read),
                    mmapName(photo.FileName),
                    0,
                    MemoryMappedFileAccess.Read,
                    HandleInheritability.None,
                    false);
                var img = new BitmapImage();
                Photo.Metadata metadata;
                DispatcherOperation metaSet;
                try {
                    using (var stream = new UnsafeMemoryMapStream(
                            mmap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read),
                            FileAccess.Read)) {
                        var data = stream.Stream;
                        {
                            var decoder = JpegBitmapDecoder.Create(data,
                                BitmapCreateOptions.PreservePixelFormat,
                                BitmapCacheOption.None);
                            var frames = decoder.Frames;
                            if (frames.Count < 1) {
                                throw new ArgumentException("Image contained no frame data.", nameof(photo));
                            }
                            var imgMeta = frames[0].Metadata as BitmapMetadata;
                            if (imgMeta == null) {
                                throw new NullReferenceException("Image contained no metadata");
                            }
                            metadata = Exif.GetMetadata(imgMeta);
                            metadata.Width = frames[0].PixelWidth;
                            metadata.Height = frames[0].PixelHeight;
                        }

                        data.Seek(0, SeekOrigin.Begin);
                        metaSet = photo.Dispatcher.InvokeAsync(() => photo.Set(metadata));
                        img.BeginInit();
                        img.StreamSource = data;
                        if (3 * metadata.Width > 2 * metadata.Height) {
                            img.DecodePixelWidth = 3 * ThumbnailHeight / 2;
                        } else {
                            img.DecodePixelHeight = ThumbnailHeight;
                        }
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.Rotation = Exif.OrienationToRotation(metadata.Orientation);
                        img.EndInit();
                        img.Freeze();
                    }
                    await metaSet;
                } catch {
                    mmap.Dispose();
                    throw;
                }
                if (await photo.Dispatcher.InvokeAsync(() => {
                    if (photo.Disposed) {
                        return false;
                    }
                    photo.mmap = mmap;
                    photo.ThumbImage = img;
                    return true;
                })) {
                    fullsizeReads.Add(new Tuple<Photo, Photo.Metadata>(photo, metadata));
                }
            } catch (Exception ex) {
                await photo.Dispatcher.InvokeAsync(() => {
                    MessageBox.Show(ex.ToString(),
                        string.Format("Error loading {0}\n\n{1}",
                        photo.FileName, ex),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    photo.Dispose();
                    list.Remove(photo);
                });
            }
        }

        private async static Task setImage(Photo photo, Photo.Metadata metadata, bool mustLock) {
            bool locked = false;
            try {
                if (mustLock) {
                    // To avoid dispose colliding with setImage.
                    await photo.loadLock.WaitAsync();
                    locked = true;
                }
                var data = await photo.Dispatcher.InvokeAsync(
                    () => (photo.mmap, photo.fullImageStream));
                if (data.mmap == null) {
                    // disposed
                    return;
                }
                if (data.fullImageStream == null) {
                    data.fullImageStream = new UnsafeMemoryMapStream(
                        data.mmap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read),
                        FileAccess.Read);
                    await photo.Dispatcher.InvokeAsync(() => {
                        var p = Interlocked.CompareExchange(ref photo.fullImageStream, data.fullImageStream, null);
                        if (p != null) {
                            data.fullImageStream.Dispose();
                            data.fullImageStream = p;
                        }
                    });
                }
                var img = new BitmapImage();
                try {
                    img.BeginInit();
                    img.StreamSource = data.fullImageStream.Stream;
                    makeFullImage(metadata, img);
                } catch (Exception ex) {
                    await photo.Dispatcher.InvokeAsync(() => {
                        MessageBox.Show(ex.ToString(),
                            string.Format("Error loading {0}\n\n{1}",
                            photo.FileName, ex),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                    return;
                }
                await photo.Dispatcher.InvokeAsync(() => {
                    photo.FullImage = img;
                });
            } finally {
                if (locked) {
                    photo.loadLock.Release();
                }
            }
        }

        private static void makeFullImage(Photo.Metadata metadata, BitmapImage img) {
            if (metadata.Width > SystemParameters.MaximizedPrimaryScreenWidth ||
                                metadata.Height > SystemParameters.MaximizedPrimaryScreenHeight) {
                if (metadata.Width * SystemParameters.MaximizedPrimaryScreenHeight >
                    metadata.Height * SystemParameters.MaximizedPrimaryScreenWidth) {
                    img.DecodePixelWidth = (int)SystemParameters.MaximizedPrimaryScreenWidth;
                } else {
                    img.DecodePixelHeight = (int)SystemParameters.MaximizedPrimaryScreenHeight;
                }
            }
            img.CacheOption = BitmapCacheOption.None;
            img.Rotation = Exif.OrienationToRotation(metadata.Orientation);
            img.EndInit();
            img.Freeze();
        }

        public static async Task Commit(Photo photo, string destination = null) {
            var tempFile = photo.FileName + DateTime.Now.Ticks.ToString() + ".tmp";
            if (destination != null) {
                tempFile = destination;
            }
            Photo.Metadata newSource;
            using (var mmap = MemoryMappedFile.OpenExisting(
                mmapName(photo.FileName),
                MemoryMappedFileRights.Read)) {
                using (var stream = new UnsafeMemoryMapStream(
                            mmap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read),
                            FileAccess.Read)) {
                    BitmapFrame sourceFrame;
                    Guid format;
                    int width, height;
                    {
                        var decoder = JpegBitmapDecoder.Create(stream.Stream,
                                    BitmapCreateOptions.PreservePixelFormat,
                                    BitmapCacheOption.None) as JpegBitmapDecoder;
                        var frames = decoder.Frames;
                        if (frames.Count < 1) {
                            await photo.Dispatcher.InvokeAsync(() => {
                                MessageBox.Show("Invalid image data",
                                    string.Format("Image {0} did not contain any frames.",
                                        photo.FileName),
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            });
                            return;
                        }
                        sourceFrame = frames[0];
                        format = decoder.CodecInfo.ContainerFormat;
                        width = sourceFrame.PixelWidth;
                        height = sourceFrame.PixelHeight;
                    }
                    var md = sourceFrame.Metadata.Clone() as BitmapMetadata;
                    newSource = await Exif.SetMetadata(photo, md);
                    newSource.Width = width;
                    newSource.Height = height;
                    var encoder = JpegBitmapEncoder.Create(format);
                    encoder.Frames.Add(
                        BitmapFrame.Create(
                            sourceFrame.Clone() as BitmapFrame,
                            sourceFrame.Thumbnail,
                            md,
                            sourceFrame.ColorContexts));
                    sourceFrame = null;
                    using (var outFile = new FileStream(tempFile, FileMode.CreateNew)) {
                        encoder.Save(outFile);
                    }
                }
            }
            if (destination == null) {
                var oldFile = tempFile + "2";
                try {
                    if (File.Exists(photo.FileName) && File.Exists(tempFile)) {
                        File.Move(photo.FileName, oldFile);
                    }
                    File.Move(tempFile, photo.FileName);
                } catch (Exception ex) {
                    // Make sure the temp file is deleted.
                    if (File.Exists(tempFile) && File.Exists(photo.FileName)) {
                        File.Delete(tempFile);
                    }
                    await photo.Dispatcher.InvokeAsync(() => {
                        MessageBox.Show(ex.ToString(),
                                string.Format("Error overwriting {0}\n\n{1}",
                            photo.FileName, ex),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                    return;
                }
                await photo.Dispatcher.InvokeAsync(() => photo.Set(newSource));

                if (File.Exists(oldFile)) {
                    // Reload the image so we can close the old file.
                    await reloadAndRemove(photo, newSource, oldFile);
                }
            }
        }

        /// <summary>
        /// Remove <paramref name="oldFile"/> after closing any handles
        /// <paramref name="photo"/> might have open for it and replacing them
        /// with new ones.
        /// </summary>
        private static async Task reloadAndRemove(Photo photo, Photo.Metadata metadata, string oldFile) {
            var oldMmap = await photo.Dispatcher.InvokeAsync(() => {
                if (photo.Disposed) {
                    return (mmap: null, stream: null);
                }
                return (mmap: Interlocked.Exchange(ref photo.mmap, null),
                        stream: Interlocked.Exchange(ref photo.fullImageStream, null));
            });
            if (oldMmap.mmap != null) {
                try {
                    if (oldMmap.stream != null) {
                        oldMmap.stream.Dispose();
                    }
                } finally {
                    oldMmap.mmap.Dispose();
                }
                try {
                    var mmap = MemoryMappedFile.CreateFromFile(
                        File.Open(photo.FileName,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Delete | FileShare.Read),
                        mmapName(photo.FileName),
                        0,
                        MemoryMappedFileAccess.Read,
                        HandleInheritability.None,
                        false);
                    if (await photo.Dispatcher.InvokeAsync(() => {
                        if (photo.Disposed) {
                            return false;
                        }
                        photo.mmap = mmap;
                        return true;
                    })) {
                        await setImage(photo, metadata, false);
                    } else {
                        mmap.Dispose();
                    }
                } catch (Exception ex) {
                    await photo.Dispatcher.InvokeAsync(() => {
                        MessageBox.Show(ex.ToString(),
                                string.Format("Reloading {0}\n\n{1}",
                            photo.FileName, ex),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
            }
            try {
                File.Delete(oldFile);
            } catch (Exception ex) {
                await photo.Dispatcher.InvokeAsync(() => {
                    MessageBox.Show(ex.ToString(),
                            string.Format("Error overwriting {0}\n\n{1}",
                        photo.FileName, ex),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }
    }
}
