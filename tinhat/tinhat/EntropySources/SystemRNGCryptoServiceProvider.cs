using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace tinhat.EntropySources
{
    /// <summary>
    /// This is literally just a wrapper around System.Security.Cryptography.RNGCryptoServiceProvider, and adds no value 
    /// whatsoever, except to serve as a placeholder in the "EntropySources" folder, so you remember to consider 
    /// RNGCryptoServiceProvider explicitly as an entropy source.
    /// </summary>
    public sealed class SystemRNGCryptoServiceProvider : RandomNumberGenerator
    {
        private RNGCryptoServiceProvider myRNG = new RNGCryptoServiceProvider();
        private bool disposed = false;
        public override void GetBytes(byte[] data)
        {
            myRNG.GetBytes(data);
        }
        public override void GetNonZeroBytes(byte[] data)
        {
            myRNG.GetNonZeroBytes(data);
        }
        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }
            myRNG.Dispose();
            base.Dispose(disposing);
        }
        ~SystemRNGCryptoServiceProvider()
        {
            Dispose(false);
        }
    }
}
