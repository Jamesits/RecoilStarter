using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace RecoilStarter
{
    public class ManagedFileHasher : IDisposable
    {
        private readonly string basePath;

        // synchronization
        private readonly Semaphore ioSemaphore;
        private int waitGroup = 0;

        // stat
        public long FileCount = 0;
        public long FileSize = 0;

        // weight factors
        private readonly double fileSizeStepBreakPoint;

        // buffer size calculation
        // https://github.com/dotnet/runtime/discussions/74405#discussioncomment-3488674
        // pipeAmplificationFactor is 1 plus your runtime's IO overhead.
        // The value depends on your runtime; on .Net Framework 3.5 (Windows 11 22H1), it should be set to around 1.3.
        private const double pipeAmplificationFactor = 1.3;
        // Per-file buffer size. Too small and you get bad performance on the I/O side; too big and you get bad performance
        // on the runtime side when reading large files. Values between 81920 and 131072 is optimal for a lot files around
        // a few MiBs and occasional large files.
        private readonly int perFileBufferSizeBytes = 81920; 
        private readonly int ioQueueDepth; // calculated

        // pipeFatness: disk sequential throughput * RTT
        // randomAccessPreference: if file size exceeds N*randomAccessPreference*1MiB, the degree of parallelism will be lowered by 2^N
        public ManagedFileHasher(string path, double pipeFatness, double randomAccessPreference)
        {
            this.basePath = path;

            fileSizeStepBreakPoint = randomAccessPreference * 1048576;
            ioQueueDepth = (int)Math.Ceiling(pipeAmplificationFactor * pipeFatness / perFileBufferSizeBytes);
            ioSemaphore = new Semaphore(ioQueueDepth, ioQueueDepth);
        }

        // calculate random access penality on large files
        private int GetWeight(long length)
        {
            int weight = (int)Math.Ceiling(length / fileSizeStepBreakPoint);
            if (weight > ioQueueDepth) weight = ioQueueDepth;
            if (weight < 1) weight = 1;
            return weight;
        }

        public void Dispose()
        {
            ioSemaphore.Close();
        }

        private struct HashRequest
        {
            public string path;
            public Stream stream;
            public int weight;
        }

        public void Run() {
            foreach (var path in EnumerateFiles(basePath))
            {
                var fi = new FileInfo(path);
                FileCount++;
                FileSize += fi.Length;

                var weight = GetWeight(fi.Length);
                for (var i = 0; i < weight; i++) ioSemaphore.WaitOne();
                Interlocked.Increment(ref waitGroup);
                ThreadPool.QueueUserWorkItem(HashProc, new HashRequest{ 
                    path = path,
                    weight = weight,
                    // DO NOT enable async here: https://adamsitnik.com/files/Fast_File_IO_with_DOTNET_6.pdf
                    stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, perFileBufferSizeBytes, FileOptions.SequentialScan),
                });
            }

            Console.Error.WriteLine("[*] All requests have been fired, collecting results...");
            while (waitGroup != 0)
            {
                //Console.WriteLine(string.Format("[i] Waiting for I/O to finish, in flight requests: {0}", waitGroup));
                Thread.Sleep(0); // Thread.Yield is not available yet
            }
            Console.Error.WriteLine("[+] Hash finished.");
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

            ioSemaphore.Release(req.Value.weight);
            req.Value.stream.Dispose();
            Console.WriteLine(string.Format("{0} {1}", req.Value.path, MD5Hasher.Hash.AsHexString()));
            MD5Hasher.Clear(); // dispose the hash result

            Interlocked.Decrement(ref waitGroup);
        }
    }
}
