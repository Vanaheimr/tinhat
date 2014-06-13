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
            // By simply instantiating each entropy source once, and discarding it, we are forcing them to run their static 
            // constructors.
            var threadRNG = new EntropySources.ThreadSchedulerRNG();
            threadRNG.Dispose();
            var threadedSeedRNG = new EntropySources.ThreadedSeedGeneratorRNG();
            threadedSeedRNG.Dispose();
            var systemRNG = new EntropySources.SystemRNGCryptoServiceProvider();
            systemRNG.Dispose();
        }
    }
}
