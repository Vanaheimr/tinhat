using System;

namespace tinhat
{
    /// <summary>
    /// Starts all entropy sources filling their respective entropy pools, to reduce wait time when you actually call them.
    /// It is recommended to use StartEarly.StartFillingEntropyPools(); at the soonest entry point to your application, for example the 
    /// first line of Main()
    /// </summary>
    public static class StartEarly
    {
        /// <summary>
        /// Starts all entropy sources filling their respective entropy pools, to reduce wait time when you actually call them.
        /// It is recommended to use StartEarly.StartFillingEntropyPools(); at the soonest entry point to your application, for example the 
        /// first line of Main()
        /// </summary>
        public static void StartFillingEntropyPools()
        {
            /* We could instantiate each of these individually - as we formerly did - 
             * But we could also just instantiate TinHatURandom() once, which will instantiate TinHatRandom, 
             * which will instantiate everything else that it uses by default.
             * So let's do that.
             * 
            var threadRNG = new EntropySources.ThreadSchedulerRNG();
            threadRNG.Dispose();
            var threadedSeedRNG = new EntropySources.ThreadedSeedGeneratorRNG();
            threadedSeedRNG.Dispose();
            var systemRNG = new EntropySources.SystemRNGCryptoServiceProvider();
            systemRNG.Dispose();
            // Also by referencing TinHatRandom.StaticInstance once, we force it to be created
            var junkString = TinHatRandom.StaticInstance.ToString();
             */

            // Just do anything that references StaticInstance, in order to make StaticInstance run through its
            // static constructor stuff
            string junkString = TinHatURandom.StaticInstance.ToString();
        }
    }
}
