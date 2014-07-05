using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Digests;

namespace tinhat
{
    /// <summary>
    /// TinHatRandom returns cryptographically strong random data, never to exceed the number of bytes available from 
    /// the specified entropy sources.  This can cause slow generation, and is recommended only for generating extremely
    /// strong keys and other things that don't require a large number of bytes quickly.  This is CPU intensive, and perhaps 
    /// generates a few KB/sec.  For general purposes, see TinHatURandom instead.
    /// </summary>
    /// <remarks>
    /// TinHatRandom returns cryptographically strong random data, never to exceed the number of bytes available from 
    /// the specified entropy sources.  This can cause slow generation, and is recommended only for generating extremely
    /// strong keys and other things that don't require a large number of bytes quickly.  This is CPU intensive, and perhaps 
    /// generates a few KB/sec.  For general purposes, see TinHatURandom instead.
    /// </remarks>
    /// <example><code>
    /// static void Main(string[] args)
    /// {
    ///     StartEarly.StartFillingEntropyPools();
    /// 
    ///     const int blockSize = 640;
    ///
    ///     // On my system, this generated about 15-60 KiB/sec
    ///     // default TinHatRandom() constructor uses:
    ///     //     SystemRNGCryptoServiceProvider/SHA256, 
    ///     //     ThreadedSeedGeneratorRNG/SHA256/RipeMD256Digest,
    ///     //     (if available) EntropyFileRNG/SHA256
    ///     using (var rng = new TinHatRandom())
    ///     {
    ///         for (int i = 0; i &lt; 125; i++)
    ///         {
    ///             var randomBytes = new byte[blockSize];
    ///             rng.GetBytes(randomBytes);
    ///         }
    ///     }
    /// }
    /// </code></example>
    public sealed class TinHatRandom : RandomNumberGenerator
    {
        private List<SupportingClasses.EntropyHasher> EntropyHashers;
        private int HashLengthInBytes;
        public TinHatRandom()
        {
            this.EntropyHashers = new List<SupportingClasses.EntropyHasher>();

            // Add the .NET implementation of SHA256 and RNGCryptoServiceProvider
            {
                var RNG = new EntropySources.SystemRNGCryptoServiceProvider();
                var HashWrapper = new SupportingClasses.HashAlgorithmWrapper(SHA256.Create());
                this.EntropyHashers.Add(new SupportingClasses.EntropyHasher(RNG, HashWrapper));
            }

            // Add the ThreadedSeedGeneratorRNG as entropy source, and chain SHA256 and RipeMD256 as hash algorithms
            {
                var RNG = new EntropySources.ThreadedSeedGeneratorRNG();
                var HashWrappers = new List<SupportingClasses.HashAlgorithmWrapper>();
                HashWrappers.Add(new SupportingClasses.HashAlgorithmWrapper(SHA256.Create()));
                HashWrappers.Add(new SupportingClasses.HashAlgorithmWrapper(new RipeMD256Digest()));
                this.EntropyHashers.Add(new SupportingClasses.EntropyHasher(RNG, HashWrappers));
            }

            // If available, add EntropyFileRNG as entropy source
            {
                EntropySources.EntropyFileRNG RNG = null;
                try
                {
                    RNG = new EntropySources.EntropyFileRNG();
                }
                catch { }   // EntropyFileRNG thows exceptions if it hasn't been seeded yet, if it encouters corruption, etc.
                if (RNG != null)
                {
                    var HashWrapper = new SupportingClasses.HashAlgorithmWrapper(SHA256.Create());
                    this.EntropyHashers.Add(new SupportingClasses.EntropyHasher(RNG, HashWrapper));
                }
            }

            CtorSanityCheck();
        }
        public TinHatRandom(List<SupportingClasses.EntropyHasher> EntropyHashers)
        {
            this.EntropyHashers = EntropyHashers;
            CtorSanityCheck();
        }
        private void CtorSanityCheck()
        {
            if (EntropyHashers == null)
            {
                throw new ArgumentNullException("EntropyHashers");
            }
            if (EntropyHashers.Count < 1)
            {
                throw new ArgumentException("EntropyHashers.Count cannot be < 1");
            }
            int HashLengthInBits = -1;
            foreach (SupportingClasses.EntropyHasher eHasher in EntropyHashers)
            {
                if (eHasher.RNG == null)
                {
                    throw new ArgumentException("RNG cannot be null");
                }
                if (eHasher.HashWrappers == null)
                {
                    throw new ArgumentException("HashWrappers cannot be null");
                }
                if (eHasher.HashWrappers.Count < 1)
                {
                    throw new ArgumentException("HashWrappers.Count cannot be < 1");
                }
                foreach (SupportingClasses.HashAlgorithmWrapper hashWrapper in eHasher.HashWrappers)
                {
                    if (HashLengthInBits == -1)
                    {
                        HashLengthInBits = hashWrapper.HashSizeInBits;
                    }
                    else
                    {
                        if (HashLengthInBits != hashWrapper.HashSizeInBits)
                        {
                            throw new ArgumentException("Hash functions must all return the same size digest");
                        }
                    }
                }
            }
            HashLengthInBytes = HashLengthInBits / 8;
        }
        private byte[] CombineByteArrays(List<byte[]> byteArrays)
        {
            if (byteArrays == null)
                throw new ArgumentNullException("byteArrays");
            if (byteArrays.Count < 1)
                throw new ArgumentException("byteArrays.Count < 1");

            byte[] accumulator = new byte[HashLengthInBytes];
            if (byteArrays.Count == 1)
            {
                if (byteArrays[0].Length != HashLengthInBytes)
                    throw new ArgumentException("byteArray.Length != HashLengthInBytes");
                Array.Copy(byteArrays[0], accumulator, HashLengthInBytes);
            }
            else // if (byteArrays.Count > 1)
            {
                Array.Clear(accumulator, 0, accumulator.Length);    // Should be unnecessary, but just to make sure.
                foreach (byte[] byteArray in byteArrays)
                {
                    if (byteArray.Length != HashLengthInBytes)
                        throw new ArgumentException("byteArray.Length != HashLengthInBytes");
                    for (int i = 0; i < HashLengthInBytes; i++)
                    {
                        accumulator[i] ^= byteArray[i];
                    }
                }
            }
            return accumulator;
        }
        private bool CompareByteArrays(byte[] first, byte[] second)
        {
            if (first == null || second == null)
                throw new CryptographicException("null byte array in allByteArraysThatMustBeUnique");
            if (first.Length != HashLengthInBytes || second.Length != HashLengthInBytes)
                throw new CryptographicException("byte array in allByteArraysThatMustBeUnique with wrong length");
            for (int i = 0; i < HashLengthInBytes; i++)
            {
                if (first[i] != second[i])
                    return false;
            }
            return true;
        }
        public override void GetBytes(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            if (data.Length == 0)
            {
                return;
            }
            int pos = 0;
            bool finished = false;
            while (false == finished)
            {
                List<byte[]> allByteArraysThatMustBeUnique = new List<byte[]>();
                List<byte[]> outputs = new List<byte[]>();
                foreach (SupportingClasses.EntropyHasher eHasher in EntropyHashers)
                {
                    List<byte[]> hashes = new List<byte[]>();
                    byte[] entropy = new byte[HashLengthInBytes];
                    eHasher.RNG.GetBytes(entropy);
                    allByteArraysThatMustBeUnique.Add(entropy);
                    if (eHasher.HashWrappers == null || eHasher.HashWrappers.Count < 1)
                        throw new CryptographicException("eHasher.HashWrappers == null || eHasher.HashWrappers.Count < 1");
                    foreach (SupportingClasses.HashAlgorithmWrapper hasher in eHasher.HashWrappers)
                    {
                        byte[] hash = hasher.ComputeHash(entropy);
                        hashes.Add(hash);
                        allByteArraysThatMustBeUnique.Add(hash);
                    }
                    // We don't bother comparing any of the hashes for equality right now, because the big loop
                    // will do that later, when checking allByteArraysThatMustBeUnique.
                    if (hashes.Count == 1)
                    {
                        // We don't need to combine hashes, if there is only one hash.
                        // No need to allByteArraysThatMustBeUnique.Add, because that was already done above.
                        outputs.Add(hashes[0]);
                    }
                    else if (hashes.Count > 1)
                    {
                        byte[] output = CombineByteArrays(hashes);
                        allByteArraysThatMustBeUnique.Add(output);
                        outputs.Add(output);
                    }
                    else
                    {
                        // Impossible to get here because foreach() loops over eHasher.HashWrappers and does "hashes.Add" on each
                        // iteration.  And eHasher.HashWrappers was already checked for null and checked for Count < 1
                        throw new Exception("Impossible Exception # A0B276734D");
                    }
                }
                byte[] finalOutput = CombineByteArrays(outputs);
                allByteArraysThatMustBeUnique.Add(finalOutput);
                for (int i = 0; i < allByteArraysThatMustBeUnique.Count - 1; i++)
                {
                    byte[] firstByteArray=allByteArraysThatMustBeUnique[i];
                    for (int j=i+1; j<allByteArraysThatMustBeUnique.Count; j++)
                    {
                        byte[] secondByteArray=allByteArraysThatMustBeUnique[j];
                        if (CompareByteArrays(firstByteArray, secondByteArray))
                        {
                            // If we get here, it means a collision has been detected.  Assuming users are only using this library
                            // for crypto random, 128 bits and higher, this is implausible to ever happen by accident.
                            // In our readme, we say "detect and correct the case of identical output: Just compare 
                            // the outputs, and if they're equal, discard one of them."
                            // In reality, we go much more dramatic than that.  Not only the outputs, but all the entropy inputs,
                            // and their intermediate hashes, are all expected to be unique.  And if we ever find anything non-unique,
                            // rather than quietly discarding it, throw exception.
                            throw new CryptographicException("non-unique arrays in allByteArraysThatMustBeUnique");
                        }
                    }
                }
                for (int i = 0; i < finalOutput.Length; i++)    // copy the finalOutput to the requested user buffer
                {
                    data[pos] = finalOutput[i];
                    pos++;
                    if (pos == data.Length)
                    {
                        finished = true;
                        break;
                    }
                }
                foreach (byte[] byteArray in allByteArraysThatMustBeUnique)
                {
                    Array.Clear(byteArray, 0, byteArray.Length);
                }
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
        protected override void Dispose(bool disposing)
        {
            if (EntropyHashers != null)
            {
                List<SupportingClasses.EntropyHasher> myHashers = EntropyHashers;
                EntropyHashers = null;
                try
                {
                    foreach (SupportingClasses.EntropyHasher hasher in myHashers)
                    {
                        try
                        {
                            ((IDisposable)hasher).Dispose();
                        }
                        catch { }
                    }
                }
                catch {}
            }
            base.Dispose(disposing);
        }
        ~TinHatRandom()
        {
            Dispose(false);
        }
    }
}
