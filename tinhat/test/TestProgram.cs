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
            StartEarly.StartFillingEntropyPools();

            const int blockSize = 640;
            var randomBytes = new byte[blockSize];

            ulong byteCount;
            DateTime before;
            DateTime after;
            double byteRate;

            for (int loopCount = 0; loopCount < 3; loopCount++)
            {
                System.Console.Error.WriteLine("---------------- loopCount " + loopCount.ToString());

                // On my system, this generated about 15-60 KiB/sec
                System.Console.Error.Write("TinHatRandom.GetBytes: ");
                byteCount = 0;
                using (var rng = new TinHatRandom())
                {
                    before = DateTime.Now;
                    for (int i = 0; i < 125; i++)
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

                // On my system, this generated about 2-6 MiB/sec
                System.Console.Error.Write("TinHatURandom.GetBytes: ");
                byteCount = 0;
                using (var urng = new TinHatURandom())
                {
                    before = DateTime.Now;
                    for (int i = 0; i < 32000; i++)
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

                // On my system, this generated about 15-60 KiB/sec
                System.Console.Error.Write("TinHatRandom.GetNonZeroBytes: ");
                byteCount = 0;
                using (var rng = new TinHatRandom())
                {
                    before = DateTime.Now;
                    for (int i = 0; i < 125; i++)
                    {
                        byteCount += blockSize;
                        rng.GetNonZeroBytes(randomBytes);
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

                // On my system, this generated about 2-6 MiB/sec
                System.Console.Error.Write("TinHatURandom.GetNonZeroBytes: ");
                byteCount = 0;
                using (var urng = new TinHatURandom())
                {
                    before = DateTime.Now;
                    for (int i = 0; i < 32000; i++)
                    {
                        byteCount += blockSize;
                        urng.GetNonZeroBytes(randomBytes);
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
            System.Threading.Thread.Sleep(int.MaxValue);
        }
    }
}
