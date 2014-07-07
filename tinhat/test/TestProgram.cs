using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using tinhat;

namespace test
{
    class TestProgram
    {
        static void Main(string[] args)
        {
            // It's always good to StartFillingEntropyPools as early as possible when the application is launched.
            StartEarly.StartFillingEntropyPools();

            // Below, I'm going to benchmark RNG generation.  Allocate a buffer first, which will be reused.
            const int blockSize = 640;
            var randomBytes = new byte[blockSize];

            TinHatRandom.StaticInstance.GetBytes(randomBytes);
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randomBytes);
            TinHatURandom.StaticInstance.GetBytes(randomBytes);
            tinhat.EntropySources.EntropyFileRNG.AddSeedMaterial(randomBytes);

            // variables I use to measure the speed of random generation below
            ulong byteCount;
            DateTime before;
            DateTime after;
            double byteRate;

            for (int loopCount = 0; loopCount < 30; loopCount++)
            {
                System.Console.Error.WriteLine("---------------- loopCount " + loopCount.ToString());

                // Benchmark TinHatRandom.StaticInstance
                // On my system, this generated about 15-60 KiB/sec
                System.Console.Error.Write("TinHatRandom.StaticInstance.GetBytes:  ");
                byteCount = 0;
                {
                    before = DateTime.Now;
                    for (int i = 0; i < 5; i++)
                    {
                        byteCount += blockSize;
                        TinHatRandom.StaticInstance.GetBytes(randomBytes);
                    }
                    after = DateTime.Now;
                }
                byteRate = byteCount / (after - before).TotalSeconds;
                if (byteRate > 1000000)
                    System.Console.Error.WriteLine((byteRate / 1000000).ToString("F2") + " MiB/sec");
                else if (byteRate > 1000)
                    System.Console.Error.WriteLine((byteRate / 1000).ToString("F2") + " KiB/sec");
                else
                    System.Console.Error.WriteLine(byteRate.ToString("F2") + " B/sec");

                // Benchmark TinHatRandom
                // On my system, this generated about 15-60 KiB/sec
                System.Console.Error.Write("TinHatRandom.GetBytes:                 ");
                byteCount = 0;
                using (var rng = new TinHatRandom())
                {
                    before = DateTime.Now;
                    for (int i = 0; i < 5; i++)
                    {
                        byteCount += blockSize;
                        rng.GetBytes(randomBytes);
                    }
                    after = DateTime.Now;
                }
                byteRate = byteCount / (after - before).TotalSeconds;
                if (byteRate > 1000000)
                    System.Console.Error.WriteLine((byteRate / 1000000).ToString("F2") + " MiB/sec");
                else if (byteRate > 1000)
                    System.Console.Error.WriteLine((byteRate / 1000).ToString("F2") + " KiB/sec");
                else
                    System.Console.Error.WriteLine(byteRate.ToString("F2") + " B/sec");

                // Benchmark TinHatURandom.StaticInstance
                // On my system, this generated about 2-6 MiB/sec
                System.Console.Error.Write("TinHatURandom.StaticInstance.GetBytes: ");
                byteCount = 0;
                {
                    before = DateTime.Now;
                    for (int i = 0; i < 8192; i++)
                    {
                        byteCount += blockSize;
                        TinHatURandom.StaticInstance.GetBytes(randomBytes);
                    }
                    after = DateTime.Now;
                }
                byteRate = byteCount / (after - before).TotalSeconds;
                if (byteRate > 1000000)
                    System.Console.Error.WriteLine((byteRate / 1000000).ToString("F2") + " MiB/sec");
                else if (byteRate > 1000)
                    System.Console.Error.WriteLine((byteRate / 1000).ToString("F2") + " KiB/sec");
                else
                    System.Console.Error.WriteLine(byteRate.ToString("F2") + " B/sec");

                // Benchmark TinHatURandom
                // On my system, this generated about 2-6 MiB/sec
                System.Console.Error.Write("TinHatURandom.GetBytes:                ");
                byteCount = 0;
                using (var urng = new TinHatURandom())
                {
                    before = DateTime.Now;
                    for (int i = 0; i < 8192; i++)
                    {
                        byteCount += blockSize;
                        urng.GetBytes(randomBytes);
                    }
                    after = DateTime.Now;
                }
                byteRate = byteCount / (after - before).TotalSeconds;
                if (byteRate > 1000000)
                    System.Console.Error.WriteLine((byteRate / 1000000).ToString("F2") + " MiB/sec");
                else if (byteRate > 1000)
                    System.Console.Error.WriteLine((byteRate / 1000).ToString("F2") + " KiB/sec");
                else
                    System.Console.Error.WriteLine(byteRate.ToString("F2") + " B/sec");
            }
            System.Console.Error.WriteLine("Finished");
            System.Threading.Thread.Sleep(int.MaxValue);    // Just so the window doesn't close instantly
        }
    }
}
