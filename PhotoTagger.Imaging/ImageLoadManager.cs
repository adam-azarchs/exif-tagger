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
    public sealed class ImageLoadManager : IDisposable {

        /// <summary>
        /// Begins loading the image and metadata for the given
        /// <see cref="Photo"/>.
        /// </summary>
        /// <param name="photo">The photo to load.</param>
        /// <param name="list">If metadata loading fails, the photo is removed
        /// from this list.</param>
        public void EnqueueLoad(Photo photo,
                                ObservableCollection<Photo> list) {
            metadataReads.Add(new Tuple<Photo, ObservableCollection<Photo>>(photo, list));
            makeIOThread();
        }

        internal void EnqueueFullSizeRead(Photo photo, Photo.Metadata meta) {
            fullsizeReads.Add(new Tuple<Photo, Photo.Metadata>(photo, meta));
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
            } catch (ObjectDisposedException) {
                return;
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

        /// <summary>
        /// Gets or sets the pixel height at which thumbnails are loaded.
        /// </summary>
        public int ThumbnailHeight {
            get; set;
        } = 48;

        private async Task loadMeta(Photo photo,
                                    ObservableCollection<Photo> list) {
            try {
                if (photo.Disposed) {
                    return;
                }
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
                            var decoder = BitmapDecoder.Create(data,
                                BitmapCreateOptions.PreservePixelFormat,
                                BitmapCacheOption.None);
                            var frames = decoder.Frames;
                            if (frames.Count < 1) {
                                throw new ArgumentException("Image contained no frame data.", nameof(photo));
                            }
                            if (!(frames[0].Metadata is BitmapMetadata imgMeta)) {
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
                if (!await photo.Dispatcher.InvokeAsync(() => {
                    if (photo.Disposed) {
                        return false;
                    }
                    photo.mmap = mmap;
                    photo.loader = this;
                    photo.ThumbImage = img;
                    return true;
                })) {
                    mmap.Dispose();
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
            if (photo.Disposed) {
                return;
            }
            try {
                if (mustLock) {
                    // To avoid dispose colliding with setImage.
                    await photo.loadLock.WaitAsync();
                    locked = true;
                }
                if (photo.Disposed) {
                    return;
                }
                var (mmap, fullImageStream) = await photo.Dispatcher.InvokeAsync(
                    () => (photo.mmap, photo.fullImageStream));
                if (mmap == null) {
                    // disposed
                    return;
                }
                if (fullImageStream == null) {
                    fullImageStream = new UnsafeMemoryMapStream(
                        mmap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read),
                        FileAccess.Read);
                    await photo.Dispatcher.InvokeAsync(() => {
                        var p = Interlocked.CompareExchange(
                            ref photo.fullImageStream, fullImageStream, null);
                        if (p != null) {
                            fullImageStream.Dispose();
                            fullImageStream = p;
                        }
                    });
                }
                var img = new BitmapImage();
                try {
                    img.BeginInit();
                    img.StreamSource = fullImageStream.Stream;
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
            // Now is a good time to do a gen-0 GC to make sure the image we
            // just loaded gets promoted to at least gen1 before its caching
            // expires.
            GC.Collect(0);
        }

        /// <summary>
        /// Gets or sets the value indicating that full images should be
        /// downsampled to fit the screen.
        /// </summary>
        /// <remarks>
        /// Normally, to conserve memory, <see cref="ImageLoadManager"/> loads
        /// images at sufficient resolution to display them on the current
        /// primary display.  If this value is false, then all of the pixels
        /// are loaded for the full image.  This is preferred if it is likely
        /// that users will wish to zoom in to images, but will make it likely
        /// that images will be paged out more frequently.
        /// </remarks>
        public static bool DownsampleFullImage {
            get; set;
        } = true;

        private static void makeFullImage(Photo.Metadata metadata, BitmapImage img) {
            if (DownsampleFullImage && (
                    metadata.Width > SystemParameters.MaximizedPrimaryScreenWidth ||
                    metadata.Height > SystemParameters.MaximizedPrimaryScreenHeight)) {
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

        private class ImageLoadException : Exception {
            public ImageLoadException(string message) : base(message) { }
        }

        /// <summary>
        /// Saves the given <see cref="Photo"/> to the disk.
        /// </summary>
        /// <param name="photo">The photo to save.</param>
        /// <param name="destination">If non-null, the image is saved to the
        /// given location rather than in-place, and the Photo is not reloaded.
        /// </param>
        /// <returns></returns>
        public static async Task Commit(Photo photo, string destination = null) {
            var tempFile = destination ?? photo.FileName + DateTime.Now.Ticks.ToString() + ".tmp";
            Photo.Metadata newSource;
            try {
                newSource = await reloadAndSave(photo, destination != null, tempFile);
            } catch (ImageLoadException ex) {
                await photo.Dispatcher.InvokeAsync(() => {
                    MessageBox.Show("Invalid image data",
                        string.Format($"Image {photo.FileName} {ex.Message}.",
                            photo.FileName),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
                return;
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

        private static async Task<Photo.Metadata> reloadAndSave(
            Photo photo, bool forceJpeg, string destFile) {
            using (var mmap = MemoryMappedFile.OpenExisting(
                                mmapName(photo.FileName),
                                MemoryMappedFileRights.Read)) {
                using (var stream = new UnsafeMemoryMapStream(
                            mmap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read),
                            FileAccess.Read)) {
                    (var sourceFrame, var format) = loadFrame(stream.Stream);
                    var md = sourceFrame.Metadata.Clone() as BitmapMetadata;
                    Photo.Metadata newMetadata = await Exif.SetMetadata(photo, md);
                    newMetadata.Width = sourceFrame.PixelWidth;
                    newMetadata.Height = sourceFrame.PixelHeight;
                    BitmapEncoder encoder = forceJpeg ?
                        new JpegBitmapEncoder() :
                        BitmapEncoder.Create(format);
                    encoder.Frames.Add(
                        BitmapFrame.Create(
                            sourceFrame,
                            null,
                            md,
                            sourceFrame.ColorContexts));
                    if (encoder is JpegBitmapEncoder jpg) {
                        jpg.Rotation = Exif.SaveRotation(md);
                    }
                    sourceFrame = null;
                    using (var outFile = new FileStream(destFile, FileMode.CreateNew)) {
                        encoder.Save(outFile);
                    }
                    return newMetadata;
                }
            }
        }

        private static ValueTuple<BitmapFrame, Guid> loadFrame(Stream stream) {
            var decoder = BitmapDecoder.Create(stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.None);
            var frames = decoder.Frames;
            if (frames.Count < 1) {
                throw new ImageLoadException("did not contain any frames");
            }
            var sourceFrame = frames[0];
            return (sourceFrame, decoder.CodecInfo.ContainerFormat);
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

        /// <summary>
        /// Close the object to new loads.
        /// </summary>
        public void Dispose() {
            fullsizeReads.Dispose();
            metadataReads.Dispose();
        }
    }
}
