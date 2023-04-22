using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace RecoilStarter
{
    public class ManagedFileHasher : IDisposable
    {
        // weight factors
        private readonly double fileSizeStepBreakPoint = 20 * 1024 * 1024;

        // buffer size
        // https://github.com/dotnet/runtime/discussions/74405#discussioncomment-3488674
        private readonly int perFileBufferSizeBytes = 131072;
        private readonly int ioQueueDepth = 2;

        private readonly string path;

        private readonly Semaphore ioSem;
        private int wg = 0;

        public ManagedFileHasher(string path, string strategy)
        {
            this.path = path;

            switch (strategy)
            {
                case "HDD_SATA":
                    ioQueueDepth = 2;
                    break;

                case "SSD_NVME":
                    ioQueueDepth = 128;
                    break;

                default:
                    break;
            }

            ioSem = new Semaphore(ioQueueDepth, ioQueueDepth);
        }

        public void Dispose()
        {
            ioSem.Close();
        }

        private struct HashRequest
        {
            public string path;
            public Stream stream;
            public int weight;
        }

        public void Run() {
            foreach (var file in EnumerateFiles(path))
            {
                var weight = GetWeight(file);
                for (var i = 0; i < weight; i++) ioSem.WaitOne();
                Interlocked.Increment(ref wg);
                ThreadPool.QueueUserWorkItem(HashProc, new HashRequest{ 
                    path = file,
                    weight = weight,
                    // DO NOT enable async here: https://adamsitnik.com/files/Fast_File_IO_with_DOTNET_6.pdf
                    stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, perFileBufferSizeBytes, FileOptions.SequentialScan),
                });
            }

            while (wg != 0)
            {
                //Console.WriteLine(string.Format("[i] In flight requests: {0}", wg));
                Thread.Sleep(0); // Thread.Yield is not available yet
            }
            Console.WriteLine("[i] Hash finished.");
        }

        private int GetWeight(string path)
        {
            var fi = new FileInfo(path);
            int weight = (int)(Math.Ceiling(fi.Length / fileSizeStepBreakPoint));
            if (weight > ioQueueDepth) weight = ioQueueDepth;
            if (weight < 1) weight = 1;
            return weight;
        }

        private static IEnumerable<string> EnumerateFiles(string path)
        {
            // https://stackoverflow.com/a/929418

            var queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                path = queue.Dequeue();
                try
                {
                    foreach (var subDir in Directory.GetDirectories(path))
                    {
                        queue.Enqueue(subDir);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
                string[] files = null;
                try
                {
                    files = Directory.GetFiles(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        yield return files[i];
                    }
                }
            }
        }

        private void HashProc(Object stateInfo)
        {
            var req = stateInfo as HashRequest?;
            if (req == null) return;

            //Console.WriteLine(string.Format("[i] Start {0}", req.Value.path));

            var MD5Hasher = MD5.Create();
            MD5Hasher.ComputeHash(req.Value.stream);

            ioSem.Release(req.Value.weight);
            req.Value.stream.Dispose();
            Console.WriteLine(string.Format("{0} {1} {2}", req.Value.weight, req.Value.path, MD5Hasher.Hash.AsHexString()));
            // no need to dispose the hasher on .Net Framework 3.5?

            Interlocked.Decrement(ref wg);
        }
    }
}
