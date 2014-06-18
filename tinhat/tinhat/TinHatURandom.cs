﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Crypto.Digests;

namespace tinhat
{
    /// <summary>
    /// TinHatURandom returns cryptographically strong random data.  It uses a crypto prng to generate more bytes than
    /// actually available in hardware entropy, so it's about 1,000 times faster than TinHatRandom.  For general purposes, 
    /// TinHatURandom is recommended because of its performance characteristics, but for extremely strong, long-lived keys, 
    /// TinHatRandom is recommended instead.
    /// </summary>
    /// <remarks>
    /// TinHatURandom returns cryptographically strong random data.  It uses a crypto prng to generate more bytes than
    /// actually available in hardware entropy, so it's about 1,000 times faster than TinHatRandom.  For general purposes, 
    /// TinHatURandom is recommended because of its performance characteristics, but for extremely strong, long-lived keys, 
    /// TinHatRandom is recommended instead.
    /// </remarks>
    /// <example><code>
    /// static void Main(string[] args)
    /// {
    ///     StartEarly.StartFillingEntropyPools();
    /// 
    ///     const int blockSize = 640;
    ///
    ///     // On my system, this generated about 2-6 MiB/sec
    ///     // default constructor uses SystemRNGCryptoServiceProvider/SHA256, ThreadedSeedGeneratorRNG/SHA256/RipeMD256Digest
    ///     using (var rng = new TinHatURandom())
    ///     {
    ///         for (int i = 0; i &lt; 32000; i++)
    ///         {
    ///             var randomBytes = new byte[blockSize];
    ///             rng.GetBytes(randomBytes);
    ///         }
    ///     }
    /// }
    /// </code></example>
    public sealed class TinHatURandom : RandomNumberGenerator
    {
        // Interlocked cannot handle bools.  So using int as if it were bool.
        private const int TrueInt = 1;
        private const int FalseInt = 0;
        private int disposed = FalseInt;

        private DigestRandomGenerator myPrng;
        private TinHatRandom myTinHatRandom;
        private object stateCounterLockObj = new object();
        private const int RESEED_LOCKED = 1;
        private const int RESEED_UNLOCKED = 0;
        private int reseedLockInt = RESEED_UNLOCKED;

        private const int MaxBytesPerSeedSoft =   64 * 1024;    // See "BouncyCastle DigestRandomGenerator Analysis" comment
        private const int MaxStateCounterHard = 1024 * 1024;    // See "BouncyCastle DigestRandomGenerator Analysis" comment

        private int digestSize;
        private int stateCounter = MaxStateCounterHard;     // Guarantee to seed immediately on first call to GetBytes

        /// <summary>
        /// Number of TinHatRandom bytes to use when reseeding prng
        /// </summary>
        public int SeedSize;

        /* BouncyCastle DigestRandomGenerator Analysis
         * BouncyCastle DigestRandomGenerator maintains two separate but related internal states, represented by the following:
         *     byte[] seed
         *     long   seedCounter
         *     byte[] state
         *     long   stateCounter
         * The size of seed and state are both equal to the size of the digest.  I am going to refer to the digest size, in bits,
         * as "M".  The counters are obviously 64 bits each.
         * 
         * In order to generate repeated output, there would need to be a collision of stateCounter, state, and seed.  We expect a seed
         * collision every 2^(M/2) times that we cycle seed.  We expect a state collision every 2^(M/2) times that we GenerateState,
         * and stateCounter will repeat itself every 2^64 times that we call GenerateState.  This means we can never have a repeated
         * stateCounter&state&seed in less than 2^64 calls to GenerateState, and very likely, it would be much much larger than that.
         * 
         * GenerateState is called at least once for every call to NextBytes, and it's called more times, if the number of bytes reqested
         * >= digest size in bytes.  We can easily measure the number of calls to GenerateState, by counting 1+(bytes.Length/digest.Size),
         * and we want to ensure this number is always below 2^64, which is UInt64.MaxValue
         * 
         * bytes.Length is an Int32.  We can easily guarantee we'll never repeat an internal state, if we use a UInt64 to tally the
         * number of calls to GenerateState, and require new seed material before UInt64.MaxValue - Int32.MaxValue.  This is a huge number.
         * 
         * To put this in perspective, supposing a 128 bit digest, and supposing the user on average requests 8 bytes per call to NextBytes.
         * Then there is guaranteed to be no repeat state before 147 quintillion bytes (147 billion billion).  So let's just tone this 
         * down a bit, and choose thresholds that are way more conservative.
         * 
         * Completely unrelated to analysis of DigestRandomGenerator, some other prng's (fortuna) recommend new seed material in 2^20
         * iterations, due to limitations they have, which we don't have.  So let's just ensure we end up choosing thresholds that are down
         * on-par with that level, even though completely unnecessary for us, it will feel conservative and safe.
         * 
         * Let's use a plain old int to tally the number of calls to GenerateState.  We need to ensure we never overflow this counter, so
         * let's assume all digests are at least 4 bytes, and let's require new seed material every int.MaxValue/2.  This is basically 
         * 1 billion calls to NextBytes, so a few GB of random data or so.  Extremely safe and conservative.
         * 
         * But let's squish it down even more than that.  TinHatURandom performs approx 1,000 times faster than TinHatRandom.  So to 
         * maximize the sweet spot between strong security and good performance, let's only stretch the entropy 1,000,000 times at hard 
         * maximum, and 64,000 times softly suggested.  Typically, for example with Sha256, this means we'll generate up to 2MB before 
         * requesting reseed, and up to 32MB before requiring reseed.
         * 
         * Now we're super duper conservative, being zillions of times more conservative than necessary, maximally conservative to the point 
         * where we do not take an appreciable performance degradation.
         */

        public TinHatURandom()
        {
            this.myTinHatRandom = new TinHatRandom();
            IDigest digest = new Sha256Digest();
            this.myPrng = new DigestRandomGenerator(digest);
            this.digestSize = digest.GetDigestSize();
            this.SeedSize = this.digestSize;
            Reseed();
        }
        public TinHatURandom(IDigest digest)
        {
            this.myTinHatRandom = new TinHatRandom();
            this.myPrng = new DigestRandomGenerator(digest);
            this.digestSize = digest.GetDigestSize();
            this.SeedSize = this.digestSize;
            Reseed();
        }
        public TinHatURandom(List<SupportingClasses.EntropyHasher> EntropyHashers, IDigest digest)
        {
            this.myTinHatRandom = new TinHatRandom(EntropyHashers);
            this.myPrng = new DigestRandomGenerator(digest);
            this.digestSize = digest.GetDigestSize();
            this.SeedSize = this.digestSize;
            Reseed();
        }

        public override void GetBytes(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            if (data.Length == 0)
                return;
            lock (stateCounterLockObj)
            {
                int newStateCounter = this.stateCounter + 1 + (data.Length / this.digestSize);
                if (newStateCounter > MaxStateCounterHard)
                {
                    Reseed();   // Guarantees to reset stateCounter = 0
                }
                else if (newStateCounter > MaxBytesPerSeedSoft)
                {
                    if (Interlocked.Exchange(ref reseedLockInt, RESEED_LOCKED) == RESEED_UNLOCKED)    // If more than one thread race here, let the first one through, and others exit
                    {
                        // System.Console.Error.Write(".");
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ReseedCallback));
                    }
                }
                // Repeat the addition, instead of using newStateCounter, because the above Reseed() might have changed stateCounter
                this.stateCounter += 1 + (data.Length / this.digestSize);
                myPrng.NextBytes(data); // Internally, DigestRandomGenerator locks all operations, so reseeding cannot occur in the middle of NextBytes()
            }
        }
        public override void GetNonZeroBytes(byte[] data)
        {
            // Apparently, the reason for GetNonZeroBytes to exist, is sometimes people generate null-terminated salt strings.
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            if (data.Length == 0)
            {
                return;
            }
            int pos = 0;
            while (true)
            {
                byte[] tempData = new byte[(int)(1.05 * (data.Length - pos))];    // Request 5% more data than needed, to reduce the probability of repeating loop
                GetBytes(tempData);
                for (int i = 0; i < tempData.Length; i++)
                {
                    if (tempData[i] != 0)
                    {
                        data[pos] = tempData[i];
                        pos++;
                        if (pos == data.Length)
                        {
                            Array.Clear(tempData, 0, tempData.Length);
                            return;
                        }
                    }
                }
            }
        }

        private void ReseedCallback(object state)
        {
            Reseed();
        }
        private void Reseed()
        {
            var newSeed = new byte[SeedSize];
            myTinHatRandom.GetBytes(newSeed);
            lock (stateCounterLockObj)
            {
                myPrng.AddSeedMaterial(newSeed);
                this.stateCounter = 0;
                reseedLockInt = RESEED_UNLOCKED;
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref disposed, TrueInt) == TrueInt)
            {
                return;
            }
            myTinHatRandom.Dispose();
            base.Dispose(disposing);
        }
        ~TinHatURandom()
        {
            Dispose(false);
        }
    }
}