using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace tinhat.SupportingClasses
{
    public sealed class EntropyHasher : IDisposable
    {
        public RandomNumberGenerator RNG;
        public List<HashAlgorithmWrapper> HashWrappers;
        public EntropyHasher(RandomNumberGenerator RNG, HashAlgorithmWrapper HashWrapper)
        {
            this.RNG = RNG;
            this.HashWrappers = new List<HashAlgorithmWrapper>();
            this.HashWrappers.Add(HashWrapper);
        }
        public EntropyHasher(RandomNumberGenerator RNG, List<HashAlgorithmWrapper> HashWrappers)
        {
            this.RNG = RNG;
            this.HashWrappers = HashWrappers;
        }
        public void Dispose()
        {
            lock(this)      // Just in case two threads try to dispose me at the same time?  Whatev.  ;-)
            {
                if (RNG != null)
                {
                    try
                    {
                        RNG.Dispose();
                    }
                    catch { }
                    RNG = null;
                }
                if (HashWrappers != null)
                {
                    try
                    {
                        foreach(HashAlgorithmWrapper hashWrapper in HashWrappers)
                        {
                            try
                            {
                                hashWrapper.Dispose();
                            }
                            catch { }
                        }
                    }
                    catch { }
                    HashWrappers = null;
                }
            }
        }
        ~EntropyHasher()
        {
            Dispose();
        }
    }
}
