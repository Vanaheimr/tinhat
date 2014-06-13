using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;

namespace tinhat.SupportingClasses
{
    /// <summary>
    /// HashAlgorithmWrapper is an abstraction wrapper class, to contain either .NET System.Security.Cryptography.HashAlgorithm, 
    /// or Bouncy Castle Org.BouncyCastle.Crypto.IDigest, and make the user agnostic.
    /// </summary>
    public class HashAlgorithmWrapper : IDisposable
    {
        protected delegate byte[] ComputeHashDelegate(byte[] data);
        protected object HashAlgorithmObject;
        protected ComputeHashDelegate ComputeHashDelegateInstance;

        public int HashSizeInBits { get; protected set; }

        public HashAlgorithmWrapper( HashAlgorithm HashAlg )
        {
            HashAlgorithmObject = HashAlg;
            ComputeHashDelegateInstance = HashAlg.ComputeHash;
            HashSizeInBits = HashAlg.HashSize;     // HashAlg.HashSize is measured in bits
        }
        public HashAlgorithmWrapper( IDigest BCIDigest )
        {
            HashAlgorithmObject = BCIDigest;
            ComputeHashDelegateInstance = BouncyCastleComputeHashDelegateProvider;
            HashSizeInBits = BCIDigest.GetDigestSize() * 8;   // GetDigestSize() returns a number of bytes
        }

        public byte[] ComputeHash(byte[] data)
        {
            return this.ComputeHashDelegateInstance(data);
        }

        protected byte[] BouncyCastleComputeHashDelegateProvider (byte[] data)
        {
            IDigest BCIDigest = (IDigest)this.HashAlgorithmObject;
            var output = new byte[BCIDigest.GetDigestSize()];
            BCIDigest.BlockUpdate(data, 0, data.Length);
            BCIDigest.DoFinal(output, 0);
            BCIDigest.Reset();
            return output;
        }

        public void Dispose()
        {
            Dispose(true);
        }
        protected virtual void Dispose(bool disposing)
        {
            lock (this)
            {
                if (this.HashAlgorithmObject != null)
                {
                    try
                    {
                        if (this.HashAlgorithmObject is IDisposable)
                        {
                            ((IDisposable)this.HashAlgorithmObject).Dispose();
                        }
                    }
                    catch { }
                    try
                    {
                        if (this.HashAlgorithmObject is IDigest)
                        {
                            ((IDigest)this.HashAlgorithmObject).Reset();
                        }
                    }
                    catch { }
                    HashAlgorithmObject = null;
                }
            }
        }
        ~HashAlgorithmWrapper()
        {
            Dispose(false);
        }
    }
}
