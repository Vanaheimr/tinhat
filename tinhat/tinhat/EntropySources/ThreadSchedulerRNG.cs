using System;
using System.Security.Cryptography;
using System.Threading;

namespace tinhat.EntropySources
{
    /// <summary>
    /// In a multitasking OS, each individual thread never knows when it's going to be granted execution time,
    /// as many processes and threads compete for CPU cycles.  The granularity of time to wake up from sleep is
    /// something like +/- a few ms, while the granularity of DateTime.Now is Ticks, 10million per second.  Although
    /// the OS scheduler is surely deterministic, there should be a fair amount of entropy in the least significant
    /// bit of DateTime.Now.Ticks upon thread waking.  But since the OS scheduler is surely deterministic, it is
    /// not recommended to use ThreadSchedulerRNG as your only entropy source.  It is recommended to use this
    /// class ONLY in addition to other entropy sources.
    /// </summary>
    public sealed class ThreadSchedulerRNG : RandomNumberGenerator
    {
        /// <summary>
        /// ThreadSchedulerRNG will always try to fill up to MaxPoolSize bytes available for read
        /// </summary>
        public int MaxPoolSize { get; private set; }

        private object fifoStreamLock = new object();
        private SupportingClasses.FifoStream myFifoStream = new SupportingClasses.FifoStream(Zeroize: true);
        private Thread mainThread;
        private AutoResetEvent poolFullARE = new AutoResetEvent(false);

        // Interlocked cannot handle bools.  So using int as if it were bool.
        private const int TrueInt = 1;
        private const int FalseInt = 0;
        private int disposed = FalseInt;

        // Create a static instance, in the static constructor, to start building an entropy pool as early as possible.
        private static ThreadSchedulerRNG staticThreadSchedulerRNG;

        private const int chunkSize = 16;
        private byte[] chunk;
        private int chunkByteIndex = 0;
        private int chunkBitIndex = 0;
        private int chunkBitCount = 0;  // Used to detect biased sampling
        private const int minBitsSet = (int)(chunkSize * 8 * 0.2);  // If we randomly sample less than this many bits set to 1 in a chunk, discard sample
        private const int maxBitsSet = (int)(chunkSize * 8 * 0.8);  // If we randomly sample more than this many bits set to 1 in a chunk, discard sample

        static ThreadSchedulerRNG()
        {
            staticThreadSchedulerRNG = new ThreadSchedulerRNG();
        }
        public ThreadSchedulerRNG()
        {
            this.chunk = new byte[chunkSize];
            this.MaxPoolSize = 4096;
            this.mainThread = new Thread(new ThreadStart(mainThreadLoop));
            this.mainThread.IsBackground = true;    // Don't prevent application from dying if it wants to.
            this.mainThread.Start();
        }
        public ThreadSchedulerRNG(int MaxPoolSize)
        {
            this.chunk = new byte[chunkSize];
            this.MaxPoolSize = MaxPoolSize;
            this.mainThread = new Thread(new ThreadStart(mainThreadLoop));
            this.mainThread.IsBackground = true;    // Don't prevent application from dying if it wants to.
            this.mainThread.Start();
        }
        private int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                int pos = offset;
                lock (fifoStreamLock)
                {
                    while (pos < count)
                    {
                        long readCount = myFifoStream.Length;   // All the available bytes
                        if (pos + readCount >= count)
                        {
                            readCount = count - pos;    // Don't try to read more than we need
                        }
                        if (readCount > 0)
                        {
                            int bytesRead = myFifoStream.Read(buffer, pos, (int)readCount);
                            pos += bytesRead;
                        }
                        if (pos < count)
                        {
                            // We've exhausted our own pool.  Let's see if we can get more from the static instance
                            byte[] moreBytes = staticThreadSchedulerRNG.GetAvailableBytes(count - pos);
                            Array.Copy(moreBytes, 0, buffer, pos, moreBytes.Length);
                            pos += moreBytes.Length;
                            Array.Clear(moreBytes, 0, moreBytes.Length);
                            if (pos < count)
                            {
                                // mainThread and staticThreadSchedulerRNG each produce approx 1 byte every 8ms
                                // So sleep the number of bytes we need, *8ms, and /2
                                Thread.Sleep((count-pos)*8/2);
                            }
                        }
                    }
                    return count;
                }
            }
            finally
            {
                poolFullARE.Set();
            }
        }
        public byte[] GetAvailableBytes(int MaxLength)
        {
            lock (fifoStreamLock)
            {
                long availBytesCount = myFifoStream.Length;
                byte[] allBytes;
                if (availBytesCount > MaxLength)
                {
                    allBytes = new byte[MaxLength];
                }
                else // availBytesCount could be 0, or greater
                {
                    allBytes = new byte[availBytesCount];
                }
                if (availBytesCount > 0)
                {
                    Read(allBytes, 0, allBytes.Length);
                }
                return allBytes;
            }
        }
        public override void GetBytes(byte[] data)
        {
            if (Read(data,0,data.Length) != data.Length)
            {
                throw new CryptographicException("Failed to return requested number of bytes");
            }
        }
        public override void GetNonZeroBytes(byte[] data)
        {
            int offset = 0;
            while (offset < data.Length)
            {
                var newBytes = new byte[data.Length - offset];
                if (Read(newBytes,0,newBytes.Length) != newBytes.Length)
                {
                    throw new CryptographicException("Failed to return requested number of bytes");
                }
                for (int i=0; i<newBytes.Length; i++)
                {
                    if(newBytes[i] != 0)
                    {
                        data[offset] = newBytes[i];
                        offset++;
                    }
                }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref disposed,TrueInt) == TrueInt)
            {
                return;
            }
            poolFullARE.Set();
            poolFullARE.Dispose();
            myFifoStream.Dispose();
            base.Dispose(disposing);
        }
        private void mainThreadLoop()
        {
            while (disposed == FalseInt)
            {
                if (myFifoStream.Length < MaxPoolSize)
                {
                    // While running in this tight loop, consumes approx 0% cpu time.  Cannot even measure with Task Manager
                    GotBit((byte)(DateTime.Now.Ticks % 2));
                    Thread.Sleep(1);
                }
                else
                {
                    poolFullARE.WaitOne();
                }
            }
        }

        private void GotBit(byte bitByte)
        {
            if (bitByte > 1)
            {
                throw new ArgumentException("bitByte must be equal to 0 or 1");
            }
            chunk[chunkByteIndex] <<= 1;    // << operator discards msb's and zero-fills lsb's, never causes overflow
            if (bitByte == 1)
            {
                chunk[chunkByteIndex]++;    // By incrementing, we are setting the lsb to 1.
                chunkBitCount++;
            }
            chunkBitIndex++;
            if (chunkBitIndex > 7)
            {
                chunkBitIndex = 0;
                chunkByteIndex++;
                if (chunkByteIndex >= chunkSize)
                {
                    if (chunkBitCount < minBitsSet || chunkBitCount > maxBitsSet)
                    {
                        // Then the sample is apparently biased.  Throw it out.
                    }
                    else
                    {
                        myFifoStream.Write(chunk, 0, chunkSize);
                    }
                    chunkByteIndex = 0;
                    chunkBitCount = 0;
                }
            }
        }
        ~ThreadSchedulerRNG()
        {
            Dispose(false);
        }
    }
}
