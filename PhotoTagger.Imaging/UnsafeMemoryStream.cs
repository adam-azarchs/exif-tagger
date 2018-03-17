using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace PhotoTagger.Imaging {

    /// <summary>
    /// Presents a stream accessor from a memory mapped file.
    /// </summary>
    /// <remarks>
    /// Unlinke <seealso cref="MemoryMappedViewStream"/>, this memory stream
    /// allows random access.
    /// </remarks>
    unsafe class UnsafeMemoryMapStream : IDisposable {

        private MemoryMappedViewAccessor accessor;
        private byte* bufferPointer = null;

        public UnmanagedMemoryStream Stream {
            get; private set;
        }

        // Take ownership of the giveen buffer object and create a stream
        // from it.
        public UnsafeMemoryMapStream(MemoryMappedViewAccessor accessor, FileAccess access) {
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref this.bufferPointer);
            if (this.bufferPointer == null) {
                this.disposedValue = true;
            } else {
                this.accessor = accessor;
                this.Stream = new UnmanagedMemoryStream(
                    this.bufferPointer,
                    (long)accessor.SafeMemoryMappedViewHandle.ByteLength,
                    (long)accessor.Capacity,
                    access);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Stream.Dispose();
                    if (this.bufferPointer != null) {
                        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    }
                    accessor.Dispose();
                }

                Stream = null;
                accessor = null;

                disposedValue = true;
            }
        }

        ~UnsafeMemoryMapStream() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }
}
