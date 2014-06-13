using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Prng;

namespace tinhat.EntropySources
{
    /// <summary>
    /// It is an exercise for you to prompt your own user to enter characters randomly on their keyboard. After you collect that
    /// input, come use UserStringRNG.  It is estimated the user will probably not be randomly mashing their shift key.  The characters
    /// they enter will probably be confined to a field of about 36 possible chars, but they will enter a lot of patterns and repetition.
    /// So each keystroke they enter only has a random chance approximately 1/8 or 1/16.  Conservative estimates of good users actually
    /// trying to cooperate and enter random characters are about 2 or 3 bits of entropy per character.  Aggressive estimates are about 
    /// 4 or 5 bits of entropy per character.  So it's recommended to collect at least 64 characters from the user,
    /// to form a key of 128 bit strength, or 128 characters to form a key of 256 bit strength.  The UserStringRNG class does not
    /// provide any additional entropy.  It seeds a prng with the supplied string, and provides random bytes from the prng.
    /// </summary>
    public sealed class UserStringRNG : RandomNumberGenerator
    {
        // Interlocked cannot handle bools.  So using int as if it were bool.
        private const int TrueInt = 1;
        private const int FalseInt = 0;
        private int disposed = FalseInt;

        private DigestRandomGenerator myRNG;

        public UserStringRNG(string userString)
        {
            this.myRNG = new DigestRandomGenerator(new Sha256Digest());
            this.myRNG.AddSeedMaterial(Encoding.UTF8.GetBytes(userString));
        }
        public UserStringRNG(string userString, IDigest digest)
        {
            this.myRNG = new DigestRandomGenerator(digest);
            this.myRNG.AddSeedMaterial(Encoding.UTF8.GetBytes(userString));
        }
        public UserStringRNG(byte[] userBytes, IDigest digest)
        {
            this.myRNG = new DigestRandomGenerator(digest);
            this.myRNG.AddSeedMaterial(userBytes);
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
                for (int i=0; i<newBytes.Length; i++)
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
            base.Dispose(disposing);
        }
        ~UserStringRNG()
        {
            Dispose(false);
        }
    }
}
