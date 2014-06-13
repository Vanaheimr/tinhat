using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;

namespace tinhat.SupportingClasses
{
    /// <summary>
    /// Creates a stream with no backing store (ephemeral memory)
    /// </summary>
    /// <remarks>
    /// <p>This is very similar to a System.IO.MemoryStream, except, once data has been read from the FifoStream,
    /// it disappears.  You cannot seek, you cannot get or set position.  If you write to the FifoStream, the data
    /// is written to the end, and and when you read, the beginning moves closer to the end.</p>
    /// <p>Length tells you how many bytes are currently in memory.</p>
    /// <p>After calling Close(), the FifoStream cannot be written to anymore, but it can still be read from.</p>
    /// <p>The writer should call Close() when it's done writing.  The reader may optionally call Close() when it's done reading.</p>
    /// <p>After Close() has been called, the reader may read up to the number of bytes available, and subsequent calls to Read()
    /// will return 0.  Read() will never return 0, until after Close() has been called, and all the bytes have been read.</p>
    /// </remarks>
    public class FifoStream : Stream
    {
        /// <summary>
        /// If you set IOException to true, subsequent calls to Read() will throw IOException.
        /// You can only set to true.  Once you set to true, you cannot set to false.  There is no undo.
        /// Throws ArgumentException if you attempt to set false.  (Don't do it.)
        /// </summary>
        private bool _IOException = false;
        public bool IOException
        {
            get
            {
                return _IOException;
            }
            set
            {
                if (value == true)
                {
                    _IOException = true;
                    this.readerARE.Set();
                    this.flushARE.Set();
                }
                else
                {
                    throw new ArgumentException();
                }
            }
        }

        /// <summary>
        /// Specifies whether or not to zeroize ephemeral memory after it has been read.  If set to true, degrades performance
        /// somewhat.
        /// </summary>
        public bool Zeroize { get; private set; }
        private bool closed = false;
        private byte[] currentBlock = null;
        private int currentBlockPosition = 0;
        private Queue<byte[]> queue = new Queue<byte[]>();

        private object readLockObj = new object();

        private AutoResetEvent readerARE = new AutoResetEvent(false);
        private AutoResetEvent flushARE = new AutoResetEvent(false);

        public FifoStream()
        {
            Zeroize = true;
        }
        public FifoStream(bool Zeroize)
        {
            this.Zeroize = Zeroize;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return (false==this.closed); } }
        private long _length = 0;
        public override long Length { get { return _length; } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Flush()
        {
            while (this._length > 0) 
                this.flushARE.WaitOne();
            if (this._IOException)
                throw new IOException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            int finalPosition = offset + count;
            if (buffer.Length < finalPosition || offset < 0 || count < 0)
                throw new ArgumentException();
            if (this.closed || this._IOException)
            {
                throw new IOException();
            }

            var newBytes = new byte[count];
            Array.Copy(buffer, offset, newBytes, 0, count);

            lock (this.queue)
            {
                this.queue.Enqueue(newBytes);
                this._length += newBytes.Length;
                this.readerARE.Set();
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            int finalPosition = offset + count;
            if (buffer.Length < finalPosition || offset < 0 || count <= 0)
                throw new ArgumentException();
            if (this._IOException)
                throw new IOException();
            if (this.closed && this._length == 0)
            {
                return 0;
            }

            int position = offset;
            int bytesRead = 0;

            lock (this.readLockObj)
            {
                long internalLength = this._length;
                while (position < finalPosition && (false == this.closed || internalLength > 0))
                {
                    if (this.currentBlock == null)
                    {
                        lock (this.queue)
                        {
                            if (this.queue.Count > 0)
                            {
                                this.currentBlock = this.queue.Dequeue();
                            }
                        }
                    }
                    if (this.currentBlock == null)
                    {
                        if (!this.closed)
                        {
                            this.readerARE.WaitOne();
                            if (this._IOException)
                                throw new IOException();
                        }
                        continue;
                    }
                    int bytesRequested = finalPosition - position;
                    int bytesAvail = this.currentBlock.Length - this.currentBlockPosition;
                    int bytesToRead;
                    if (bytesRequested <= bytesAvail)
                    {
                        bytesToRead = bytesRequested;
                    }
                    else
                    {
                        bytesToRead = bytesAvail;
                    }
                    Array.Copy(this.currentBlock, this.currentBlockPosition, buffer, position, bytesToRead);
                    if (this.Zeroize)
                    {
                        Array.Clear(this.currentBlock, this.currentBlockPosition, bytesToRead);
                    }
                    internalLength -= bytesToRead;
                    position += bytesToRead;
                    this.currentBlockPosition += bytesToRead;
                    bytesRead += bytesToRead;
                    if (this.currentBlockPosition == this.currentBlock.Length)
                    {
                        this.currentBlock = null;
                        this.currentBlockPosition = 0;
                    }
                }
            }
            this.flushARE.Set();
            this._length -= bytesRead;
            return bytesRead;
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "writerARE")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "readerARE")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "flushARE")]
        protected override void Dispose(bool disposing)
        {
            this.closed = true;
            readerARE.Set();
            // We explicitly don't dispose these objects now, although it's considered best practice to do so.
            // We don't know if anything might be waiting on WaitOne, but if there is, we want it to continue
            // so we will wait for garbage collector to dispose these objects later.
            // readerARE.Dispose();
            // writerARE.Dispose();
            // flushARE.Dispose();
            base.Dispose(disposing);
        }
    }
}
