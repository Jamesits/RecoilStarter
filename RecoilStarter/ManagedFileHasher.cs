using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace RecoilStarter
{
    public class ManagedFileHasher : IDisposable
    {
        private const int blockSizeBytes = 4 * 1024 * 1024;
        private const int filePreloadCount = 64;
        private const int ioQueueDepth = 2;

        private readonly string path;

        private Task task;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private struct FileHashTrackingObj
        {
            public string Path;
            public byte[] Hash;

            public FileStream FileStream;
        };
        private readonly BlockingCollection<FileHashTrackingObj> openedFiles = new BlockingCollection<FileHashTrackingObj>(filePreloadCount);
        private readonly BlockingCollection<Task<FileHashTrackingObj>> hashedFiles = new BlockingCollection<Task<FileHashTrackingObj>>(ioQueueDepth);

        public ManagedFileHasher(string path)
        {
            this.path = path;
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
        }

        public Task Start() {
            // start the async operations
            List<Task> tasks = new List<Task>();

            Console.WriteLine("[i] Starting file counting thread...");
            Task fileCountingTask = new Task(() => FileCountingHelper(path, openedFiles, cts.Token));
            tasks.Add(fileCountingTask);
            fileCountingTask.Start();

            Console.WriteLine("[i] Starting file hashing thread...");
            Task fileHashingTask = new Task(() => FileReadingHelper(openedFiles, hashedFiles, cts.Token));
            tasks.Add(fileHashingTask);
            fileHashingTask.Start();

            Console.WriteLine("[i] Starting result serialization thread...");
            Task resultSerializationTask = new Task(() => FileHashSerializationHelper(hashedFiles, cts.Token));
            tasks.Add(fileHashingTask);
            resultSerializationTask.Start();
            
            return WhenAll(tasks, cts.Token);
        }

        public void Cancel()
        {
            cts.Cancel();
        }

        private static Task WhenAll(List<Task> tasks, CancellationToken token)
        {
            return Task.Factory.ContinueWhenAll(tasks.ToArray(), t => { }, token);
        }

        private static void FileCountingHelper(string path, BlockingCollection<FileHashTrackingObj> files, CancellationToken token)
        {
            // Directory.EnumerateFiles should be implemented with lazy evaluation so we can mimimize resource usage;
            // if not, see alternatives: https://stackoverflow.com/a/929418
            foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            {
                if (token.IsCancellationRequested) break;

                try
                {
                    var obj = new FileHashTrackingObj
                    {
                        Path = file,
                        // open the file
                        // https://stackoverflow.com/a/1374156
                        FileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, blockSizeBytes, FileOptions.Asynchronous | FileOptions.SequentialScan)
                    };

                    files.Add(obj, token);
                    //Console.WriteLine(file);
                } catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

            }

            files.CompleteAdding();
            Console.WriteLine("[i] FileCountingHelper exited.");
        }

        private static void FileReadingHelper(BlockingCollection<FileHashTrackingObj> files, BlockingCollection<Task<FileHashTrackingObj>> openedFiles, CancellationToken token)
        {
            while ((!token.IsCancellationRequested))
            {
                FileHashTrackingObj obj;
                try
                {
                    obj = files.Take(token);
                } catch (InvalidOperationException) // completed
                {
                    break;
                }
                
                Task<FileHashTrackingObj> task = new Task<FileHashTrackingObj>(() =>
                {
                    //byte[] buffer = new byte[blockSize];
                    //var bytes = FileStream.Read(buffer, 0, blockSize);
                    //Console.WriteLine(String.Format("read %d bytes", bytes));
                    MD5 MD5Hasher = MD5.Create();
                    MD5Hasher.ComputeHash(obj.FileStream);
                    obj.FileStream.Dispose();
                    obj.Hash = MD5Hasher.Hash;
                    MD5Hasher.Dispose();
                    return obj;
                }, token);
                openedFiles.Add(task, token);
                task.Start();
            }

            openedFiles.CompleteAdding();
            Console.WriteLine("[i] FileReadingHelper exited.");
        }

        private static void FileHashSerializationHelper(BlockingCollection<Task<FileHashTrackingObj>> openedFiles, CancellationToken token)
        {
            while ((!token.IsCancellationRequested))
            {
                Task<FileHashTrackingObj> task;
                try
                {
                    task = openedFiles.Take();
                }
                catch (InvalidOperationException) // completed
                {
                    break;
                }

                task.Wait(token);
                FileHashTrackingObj obj = task.Result;
                Console.WriteLine(String.Format("{0} => {1}", obj.Path, obj.Hash.AsHexString()));
            }

            Console.WriteLine("[i] FileHashSerializationHelper exited.");
        }
    }
}
