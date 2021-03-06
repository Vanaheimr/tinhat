﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Prng;
using System.IO;

namespace tinhat.EntropySources
{
    /// <summary>
    /// It is recommended, at least one time, to prompt user for random keyboard input, mouse input, perhaps poll some
    /// internet random sources, and every possible available entropy source, and use it to seed the EntropyFileRNG.  
    /// The EntropyFileRNG will save this to disk, and upon every time an application is launched that uses EntropyFileRNG, 
    /// the file will always be reseeded by itself, and will serve PRNG bits 
    /// </summary>
    public sealed class EntropyFileRNG : RandomNumberGenerator
    {
        private static byte[] HardCodedOptionalEntropy = new byte[] { 0x8A, 0x5E, 0x89, 0x1E, 0x56, 0x6C, 0x66, 0xFA, 0x6F, 0x62, 0x4A, 0x3B, 0x9E, 0x33, 0xC4, 0x12, 0x28, 0x92, 0x2F, 0x08, 0x9C, 0x51, 0x1F, 0x5B, 0x85, 0x86, 0x1A, 0x68, 0xEF, 0x43, 0x02 };

        /// <summary>
        /// When any instance of EntropyFileRNG adds seed material to the pool, it raises this event to signal all instances of EntropyFileRNG
        /// to reseed themselves.  Those instances may, in turn, raise their public Reseeded events.
        /// </summary>
        private static event EventHandler Reseeded_Private;

        /// <summary>
        /// Mainly intended for TinHat internal use.  Whenever user adds seed material, notify all TinHatRandom instances that didn't
        /// previously have an instance of EntropyFileRNG
        /// </summary>
        public static event EventHandler BecameAvailable;

        /// <summary>
        /// Mainly intended for TinHat internal use.  Whenever user adds seed material, all EntropyFileRNG instances will reseed themselves,
        /// and this needs to be caught by TinHatRandom so they can raise EntropyIncreased, so TinHatURandom will then reseed itself.
        /// </summary>
        public event EventHandler Reseeded;

        /// <summary>
        /// 32
        /// </summary>
        private const int HmacSha256Length = 32;

        /// <summary>
        /// The algorithm to be used by the internal PRNG to produce random output from the seed in the seed file
        /// </summary>
        public enum PrngAlgorithm : int
        {
            MD5_128bit = 1,
            RIPEMD128_128bit = 2,
            RIPEMD160_160bit = 3,
            SHA1_160bit = 4,
            Tiger_192bit = 5,
            RIPEMD256_256bit = 6,
            SHA256_256bit = 7,
            RIPEMD320_320bit = 8,
            SHA512_512bit = 9,
            Whirlpool_512bit = 10,
        }

        /// <summary>
        /// It is important to mix new seed entropy into the pool, using some sort of mixing algorithm that will neither reduce
        /// the entropy in the pool, nor allow a maliciously crafted new seed material to reduce the entropy in the pool. Many
        /// possible ways to do this could be used - we just happen to implement the ones listed here.
        /// </summary>
        public enum MixingAlgorithm : int
        {
            MD5 = 1,
            RIPEMD160 = 2,
            SHA1 = 3,
            SHA256 = 4,
            SHA512 = 5
        }

        // Interlocked cannot handle bools.  So using int as if it were bool.
        private const int TrueInt = 1;
        private const int FalseInt = 0;
        private int disposed = FalseInt;

        private MixingAlgorithm myMixingAlgorithm;
        private PrngAlgorithm myRNGAlgorithm;
        private DigestRandomGenerator myRNG;

        /* Why is PoolSize hard-coded to 3072 bytes?
         * Realistically speaking, anything in the range of 16 to 32 good quality random bytes is perfect.  Far beyond any estimates of 
         * present and future crypto cracking techniques.  But computers we use nowadays store data in blocks of 512b, 4k, or 8k 
         * on disk, so it's just a waste of space *not* to use more than 32 bytes in the pool.  The days of 512b are becoming
         * antiquated, so I'm just ignoring it.  I'm assuming a typical disk block to be 4k.  We'll add some overhead for hashing,
         * assume some overhead taken by the filesystem itself, conservatively settle on 3072 bytes as a cap, which is way crazy
         * super far beyond any realistic needs of cryptographic entropy strength.
         * 
         * One thing is absolutely undeniably clear:  If you have anywhere near 32 bytes, or 3072 bytes of entropy in your pool, your
         * cryptographic weak point will *not* be the quantity of entropy bytes you have.  Beware the $5 wrench.  http://xkcd.com/538/
         */
        /// <summary>
        /// 3072
        /// </summary>
        private const int PoolSize = 3072;

        private EventHandler EntropyFileRNG_Reseeded_Handler;

        /// <summary>
        /// Returns a single byte array containing all the bytes of all the provided arrays
        /// </summary>
        public static byte[] ConcatenateByteArrays(IEnumerable<byte[]> byteArrays)
        {
            int totalSize = 0;
            foreach (byte[] byteArray in byteArrays)
            {
                checked    // That would be crazy, if the user had that much memory here, but let's not assume.
                {
                    totalSize += byteArray.Length;
                }
            }
            var allBytes = new byte[totalSize];
            int position = 0;
            foreach (byte[] byteArray in byteArrays)
            {
                Array.Copy(byteArray, 0, allBytes, position, byteArray.Length);
                position += byteArray.Length;
            }
            return allBytes;
        }

        /// <summary>
        /// The first time you ever instantiate EntropyFileRNG, you *must* provide a newSeed.  Otherwise, CryptographicException
        /// will be thrown.  You better ensure it's at least 128 bits (16 bytes), preferably much more (>=32 bytes).  Any subsequent
        /// time you instantiate EntropyFileRNG, you may use the parameter-less constructor, and it will leverage the original seed.
        /// Whenever you provide more seed bytes, entropy is always increased.  (Does not lose previous entropy bytes.)
        /// NOTICE: byte[] newSeed will be zero'd out before returning, for security reasons.
        /// </summary>
        public EntropyFileRNG(byte[] newSeed = null, MixingAlgorithm mixingAlgorithm = MixingAlgorithm.SHA256, PrngAlgorithm prngAlgorithm = PrngAlgorithm.SHA512_512bit)
        {
            this.myMixingAlgorithm = mixingAlgorithm;
            this.myRNGAlgorithm = prngAlgorithm;

            byte[] pool;
            Initialize(out pool, this.myMixingAlgorithm, newSeed);    // Clears the newSeed before returning
            this.EntropyFileRNG_Reseeded_Handler = new EventHandler(EntropyFileRNG_Reseeded);
            EntropyFileRNG.Reseeded_Private += this.EntropyFileRNG_Reseeded_Handler;

            CreateNewPRNG(pool);    // Clears pool contents before returning
        }

        /// <summary>
        /// Note:  Clears pool contents before returning
        /// </summary>
        private void CreateNewPRNG(byte[] pool)
        {
            if (pool == null)
            {
                throw new CryptographicException("Refusing to reseed with null pool");
            }
            try
            {
                if (pool.Length != PoolSize)
                {
                    throw new CryptographicException("Refusing to reseed with invalid pool");
                }
                // Now, pool has been seeded, file operations are all completed, it's time to create my internal PRNG
                IDigest digest;
                switch (this.myRNGAlgorithm)
                {
                    case PrngAlgorithm.MD5_128bit:
                        digest = new MD5Digest();
                        break;
                    case PrngAlgorithm.RIPEMD128_128bit:
                        digest = new RipeMD128Digest();
                        break;
                    case PrngAlgorithm.RIPEMD160_160bit:
                        digest = new RipeMD160Digest();
                        break;
                    case PrngAlgorithm.RIPEMD256_256bit:
                        digest = new RipeMD256Digest();
                        break;
                    case PrngAlgorithm.RIPEMD320_320bit:
                        digest = new RipeMD320Digest();
                        break;
                    case PrngAlgorithm.SHA1_160bit:
                        digest = new Sha1Digest();
                        break;
                    case PrngAlgorithm.SHA256_256bit:
                        digest = new Sha256Digest();
                        break;
                    case PrngAlgorithm.SHA512_512bit:
                        digest = new Sha512Digest();
                        break;
                    case PrngAlgorithm.Tiger_192bit:
                        digest = new TigerDigest();
                        break;
                    case PrngAlgorithm.Whirlpool_512bit:
                        digest = new WhirlpoolDigest();
                        break;
                    default:
                        throw new CryptographicException("Unknown prngAlgorithm specified: " + this.myRNGAlgorithm.ToString());
                }
                var drng = new DigestRandomGenerator(digest);
                drng.AddSeedMaterial(pool);
                this.myRNG = drng;
            }
            finally
            {
                Array.Clear(pool, 0, pool.Length);
            }
        }

        private void EntropyFileRNG_Reseeded(object sender, EventArgs e)
        {
            byte[] pool;
            Initialize(out pool, this.myMixingAlgorithm);    // Clears the newSeed before returning
            CreateNewPRNG(pool);    // Clears pool contents before returning
            if (this.Reseeded != null)
            {
                this.Reseeded(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// NOTICE: byte[] newSeed will be zero'd out before returning, for security reasons.
        /// </summary>
        public static void AddSeedMaterial(byte[] newSeed, MixingAlgorithm mixingAlgorithm = MixingAlgorithm.SHA256)
        {
            byte[] pool;
            Initialize(out pool, mixingAlgorithm, newSeed);    // Clears the newSeed before returning
            Array.Clear(pool, 0, pool.Length);
        }

        private static HashAlgorithm CreateMyHashAlgorithm(MixingAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case MixingAlgorithm.MD5:
                    return MD5.Create();
                case MixingAlgorithm.RIPEMD160:
                    return RIPEMD160.Create();
                case MixingAlgorithm.SHA1:
                    return SHA1.Create();
                case MixingAlgorithm.SHA256:
                    return SHA256.Create();
                case MixingAlgorithm.SHA512:
                    return SHA512.Create();
                default:
                    throw new ArgumentException("Unsupported algorithm");
            }
        }

        /// <summary>
        /// Opens randfile (or creates randfile, if newSeed provided and randfile nonexistent), reads in pool data, 
        /// plants newSeed (if provided), modifies and writes out randfile.
        /// NOTICE: zero's the contents of newSeed before returning.
        /// </summary>
        private static void Initialize(out byte[] pool, MixingAlgorithm mixingAlgorithm, byte[] newSeed = null)
        {
            if (newSeed != null && newSeed.Length < 8)
            {
                throw new CryptographicException("Length >= 16 would be normal.  Length 8 is lame.  Length < 8 is insane.");
            }
            HashAlgorithm myHashAlgorithm = CreateMyHashAlgorithm(mixingAlgorithm);
            try
            {
                FileStream randFileStream = OpenRandFile();
                try
                {
                    pool = new byte[PoolSize];
                    int poolPosition = 0;
                    if (randFileStream.Length == 0)
                    {
                        if (newSeed == null)
                        {
                            // Since newSeed is null, we require randFile contents.  But it's zero.  Fail.
                            throw new CryptographicException("randFile nonexistent or zero-length, and newSeed not provided. randFile must be seeded before use.");
                        }
                        else
                        {
                            // WriteRandFileContents will plant newSeed
                            WriteRandFileContents(randFileStream, newSeed, myHashAlgorithm, pool, ref poolPosition);
                        }
                    }
                    else
                    {
                        // If the file already has data in it, then we read both "pool" and "poolPosition" from it.
                        ReadRandFileContents(randFileStream, ref poolPosition, pool);
                        // WriteRandFileContents will plant newSeed, if one was provided
                        WriteRandFileContents(randFileStream, newSeed, myHashAlgorithm, pool, ref poolPosition);
                    }
                }
                finally
                {
                    randFileStream.Flush();
                    randFileStream.Close();
                }
                if (newSeed != null)
                {
                    if (EntropyFileRNG.Reseeded_Private != null)
                    {
                        EntropyFileRNG.Reseeded_Private(null, null);
                    }
                    if (EntropyFileRNG.BecameAvailable != null)
                    {
                        EntropyFileRNG.BecameAvailable(null, null);
                    }
                }
            }
            finally
            {
                myHashAlgorithm.Dispose();
                if (newSeed != null)
                {
                    Array.Clear(newSeed, 0, newSeed.Length);
                }
            }
        }
        private static void WriteRandFileContents(FileStream randFileStream, byte[] newSeed, HashAlgorithm myHashAlgorithm, byte[] pool, ref int poolPosition)
        {
            if (newSeed != null)
            {
                PlantSeed(newSeed, myHashAlgorithm, ref poolPosition, pool);
            }
            randFileStream.Position = 0;    // Truncate file
            randFileStream.SetLength(0);    // Truncate file

            // Concatenate the positionBytes and pool into "rawData" but leave enough room for HMACSHA256 at the end
            byte[] rawData = new byte[sizeof(Int32) + PoolSize + HmacSha256Length];
            byte[] positionBytes = BitConverter.GetBytes(poolPosition);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(positionBytes);
            Array.Copy(positionBytes,0,rawData,0,positionBytes.Length);

            // Every time we read pool data, we must modify it before putting it back.  So do a block-wise hash.
            int myAlgorithmSize = myHashAlgorithm.HashSize / 8; // HashSize is measured in bits.  I want bytes.
            int localPoolPosition = 0;
            while (localPoolPosition < pool.Length)
            {
                int byteCount;
                if (localPoolPosition + myAlgorithmSize <= pool.Length)
                {
                    byteCount = myAlgorithmSize;
                }
                else
                {
                    byteCount = pool.Length - localPoolPosition;
                }
                // Hash a block from the pool
                byte[] poolHash = myHashAlgorithm.ComputeHash(pool, localPoolPosition, byteCount);
                // Update the rawData
                Array.Copy(poolHash,0,rawData,localPoolPosition+sizeof(Int32),byteCount);   // Remember sizeof(Int32) offset
                localPoolPosition += byteCount;
            }

            // Now concatenate the checksum
            using (var myHmac = new HMACSHA256(HardCodedOptionalEntropy))
            {
                byte[] signature = myHmac.ComputeHash(rawData, 0, sizeof(Int32) + PoolSize);
                Array.Copy(signature, 0, rawData, sizeof(Int32) + PoolSize, HmacSha256Length);
            }

            // Now we have completed the entire process of generating rawData.
            // Protect it, write it to file, and close.
            byte[] rawDataProtected = ProtectedData.Protect(rawData, HardCodedOptionalEntropy, DataProtectionScope.CurrentUser);
            Array.Clear(rawData, 0, rawData.Length);
            randFileStream.Write(rawDataProtected, 0, rawDataProtected.Length);
            Array.Clear(rawDataProtected, 0, rawDataProtected.Length);
        }
        private static void ReadRandFileContents(FileStream randFileStream, ref int poolPosition, byte[] pool)
        {
            /* rawData is encoded as follows:
             * bytes 0-3:       4bytes, BigEndian int, poolPosition
             * bytes 4-3076:    3072bytes, pool
             * bytes 3077-3109: 32bytes, HMACSHA256 checksum of bytes 0-3076
             * 
             * Although it's impossible to guarantee integrity or good behavior in a system where an adversary can read or tamper
             * with the random file, we can at least use simple countermeasures against the simplest and most braindead adversaries.
             * Namely:  Use ProtectedData with a hard-coded OptionalEntropy, to prevent other users on the system from reading the
             * contents, and require an adversary to at least know EntropyFileRNG is the thing they're attacking, and reverse compile
             * (or read source code) to find what value we're using for OptionalEntropy.
             * 
             * Use HMACSHA256 to detect accidental corruption.  Again, this cannot guarantee we'll be alerted to *intentional* 
             * corruption, but like I said.  We're taking simple countermeasures against simple problems.
             */
            // Now read all the encrypted data into protectedRawData
            byte[] protectedRawData = new byte[randFileStream.Length];
            randFileStream.Read(protectedRawData, 0, protectedRawData.Length);
            byte[] rawData;
            try
            {
                // and decrypt to get rawData
                rawData = ProtectedData.Unprotect(protectedRawData, HardCodedOptionalEntropy, DataProtectionScope.CurrentUser);
            }
            catch (Exception e)
            {
                throw new CryptographicException("EntropyFileRNG failed to Unprotect random file", inner: e);
            }
            using (var myHmac = new HMACSHA256(HardCodedOptionalEntropy))
            {
                byte[] computedChecksum = myHmac.ComputeHash(rawData, 0, 3076);
                for (int i = 0; i < HmacSha256Length; i++)
                {
                    if (computedChecksum[i] != rawData[3076 + i])
                        throw new CryptographicException("EntropyFileRNG found corrupt random file");
                }
                // We have verified the checksum
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(rawData, 0, sizeof(Int32));
                    poolPosition = BitConverter.ToInt32(rawData, 0);
                    // We got poolPosition
                }
                Array.Copy(rawData, sizeof(Int32), pool, 0, PoolSize);
                // We got pool
            }
        }

        private static FileStream OpenRandFile()
        {
            string fileName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/tinhat.rnd";
            const int maxTries = 10000;  // Fail this many times, throw exception.
            int numTries = 0;
            while (true)
            {
                try
                {
                    return new FileStream(fileName, FileMode.OpenOrCreate , FileAccess.ReadWrite, FileShare.None);
                }
                catch
                {
                    // Most likely some other thread got there before us, has the file open, so sleep momentarily and try again.
                    // We do this silly polling and retrying, instead of using something like Mutex, because Mutex
                    // is poorly supported cross-platform.  But opening file in exclusive read/write mode, and
                    // polling, is expected to be quite reliable and compatible.
                    Thread.Sleep(1);
                }
                numTries++;
                if (numTries > maxTries)
                {
                    throw new CryptographicException("EntropyFileRNG too many failures trying to open rand file");
                }
            }
        }

        /// <summary>
        /// Mixes newSeed into pool, without reducing entropy in the pool.  Attempt to maximize entropy gained in the pool
        /// </summary>
        private static void PlantSeed(byte[] newSeed, HashAlgorithm myHashAlgorithm, ref int poolPosition, byte[] pool)
        {
            /* Let's suppose one time, the user provides 16 bytes of absolute pure uncut randomness.  The kind your grandma used to make
             * by rolling dice in the casino and tumbling numbered balls in the bingo machine and strolling around taking measurements with
             * a geiger counter (http://www.cracked.com/article_19607_the-6-most-reckless-uses-radioactive-material.html).
             * 
             * Well, a byte of data can never contain more than one byte of entropy.  So sure, if the user later provides another
             * 16 bytes of seed material, we can mix it into the first 16 bytes of the pool, and we can guarantee we don't *reduce* the 
             * entropy in the pool, but we would not be *gaining* much of anything either.  It would be foolish of us, to always
             * mix seed material in at the beginning of the pool.  The first few bytes have already been seeded.  All the remaining bytes
             * in the pool are still devoid of entropy.  Initially we could detect this by looking for patterns in the pool and overwriting
             * them, but over time, we won't know which bytes in the pool have nearly a byte of entropy in them, versus which ones have nearly
             * 0 bytes of entropy.
             * 
             * So we will keep track.  Every time user provides a newSeed, we'll mix it into the pool and also keep track of the position where
             * we left off.  Each time a newSeed is provided, we are not reducing the entropy in the pool, but we are also attempting to maximize
             * the amount of gain that we get from each provided newSeed byte.
             * 
             * Hence, we need to keep track of poolPosition
             * 
             * Our mixing operation is very straightforward.  The core priciple of tinhat revolves around hashing A, hashing B, and if they're
             * not equal, then mix them together to produce an output which is at least as random as the minimum randomness of either A or B.
             * So below, we will hash the newSeed, hash the pool, and if they're not equal, then mix them together and update the pool.  Please
             * note:  We are doing this blockwise.  *Not* hashing the entire newSeed or the entire pool at once, but instead, take a block of the
             * newSeed, a block of the pool, hash and mix, where the blocksize is equal to the hash size.  This way, we preserve as much entropy
             * as possible from both the newSeed and the pool.
             */
            int myAlgorithmSize = myHashAlgorithm.HashSize / 8; // HashSize is measured in bits.  I want bytes.
            int seedPosition = 0;
            while (seedPosition < newSeed.Length)
            {
                int byteCount;
                if (seedPosition + myAlgorithmSize <= newSeed.Length)
                {
                    byteCount = myAlgorithmSize;
                }
                else
                {
                    byteCount = newSeed.Length - seedPosition;
                }
                // Check to see if the blocks are identical.  If they are, then don't use that chunk of the newSeed.
                // This should realistically never happen, so the loop below is biased toward detecting non-identical
                // and then immediately breaking.  But for cryptographic integrity, we are obligated to check for
                // identicality.
                bool identical = true;
                {
                    int localPoolPosition = poolPosition;
                    for (int localSeedPosition = seedPosition; localSeedPosition < seedPosition + byteCount; localSeedPosition++)
                    {
                        if (newSeed[localSeedPosition] != pool[localPoolPosition])
                        {
                            identical = false;
                            break;
                        }
                        localPoolPosition++;
                        if (localPoolPosition == pool.Length)
                            localPoolPosition = 0;
                    }
                }
                if (identical)
                {
                    seedPosition += byteCount;
                }
                else
                {
                    // Hash a block from the newSeed
                    byte[] seedHash = myHashAlgorithm.ComputeHash(newSeed, seedPosition, byteCount);
                    seedPosition += byteCount;
                    // Hash a block from the pool
                    byte[] poolHash = myHashAlgorithm.ComputeHash(pool, poolPosition, byteCount);
                    // Mix the result into the pool
                    for (int i = 0; i < byteCount; i++)
                    {
                        pool[poolPosition] = (byte)(poolHash[i] ^ seedHash[i]);
                        poolPosition++;
                        if (poolPosition == pool.Length)
                            poolPosition = 0;
                    }
                }
            }
        }
        public override void GetBytes(byte[] data)
        {
            myRNG.NextBytes(data);
        }
        public override void GetNonZeroBytes(byte[] data)
        {
            int pos = 0;
            while (pos < data.Length)
            {
                byte[] newBytes = new byte[data.Length - pos];
                myRNG.NextBytes(newBytes);
                for (int i = 0; i < newBytes.Length; i++)
                {
                    if (newBytes[i] != 0)
                    {
                        data[pos] = newBytes[i];
                        pos++;
                    }
                }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref disposed, TrueInt) == TrueInt)
            {
                return;
            }
            EntropyFileRNG.Reseeded_Private -= this.EntropyFileRNG_Reseeded_Handler;
            base.Dispose(disposing);
        }
        ~EntropyFileRNG()
        {
            Dispose(false);
        }
    }
}
