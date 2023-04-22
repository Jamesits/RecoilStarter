using System;
using System.Diagnostics;
using System.Threading;

namespace RecoilStarter
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // NVMe (PCIe 4.0 x4) SSD
            var game_dir = "C:\\temp_storage_test\\Genshin Impact Game";
            var pipeFatness = 4194304; // 8000MiB/s * 500μs
            var randomAccessPreference = 256;

            // NVMe (PCIe 3.0 x4) SSD
            //var game_dir = "D:\\Program Files\\Genshin Impact\\Genshin Impact Game";
            //var pipeFatness = 1835008; // 3500MiB/s * 500μs
            //var randomAccessPreference = 256;

            // SATA SSD
            // (TBD)

            // SATA HDD
            // (TBD)

            // NAS with ZFS RAIDZ2 HDD, over 10Gbps local network
            //var game_dir = "\\\\li-nas01\\public\\dropbox\\temp_storage_test\\Genshin Impact Game";
            //var pipeFatness = 550502; // 525MiB/s * 1ms
            //var randomAccessPreference = 256;

            var hasher = new ManagedFileHasher(game_dir, pipeFatness, randomAccessPreference);
            Console.Error.WriteLine("[*] Starting hash threads...");
            var watch = Stopwatch.StartNew();
            hasher.Run();
            watch.Stop();
            Console.Error.WriteLine("[+] Hasher returned.");

            // system info
            // Note: for .net Framework 4.x, Environment.Version always return 4.0.xxxxx.yyyyy;
            // https://learn.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
            ThreadPool.GetMaxThreads(out int workerThreadsCount, out int ioThreadsCount);
            Console.Error.WriteLine(string.Format("[i] OS: {0}", Environment.OSVersion.ToString()));
            Console.Error.WriteLine(string.Format("[i] CLR Version: {0}, Workers: {1}, IO threads: {2}", Environment.Version.ToString(), workerThreadsCount, ioThreadsCount));

            // performance
            Console.Error.WriteLine(string.Format("[i] Directory: {0}", game_dir));
            Console.Error.WriteLine(string.Format("[i] Total files: {0}, Total size: {1}bytes", hasher.FileCount, hasher.FileSize));
            Console.Error.WriteLine(string.Format("[i] Time: {0:0.00}s", (double)watch.ElapsedMilliseconds / 1000));
            Console.Error.WriteLine(string.Format("[i] Speed: {0:0.00}files/s, {1:0.00}Bytes/s", (double)hasher.FileCount / watch.ElapsedMilliseconds * 1000, (double)hasher.FileSize / watch.ElapsedMilliseconds * 1000));

            if (Debugger.IsAttached)
            {
                Console.Error.WriteLine("[i] Press <Enter> to exit...");
                Console.ReadLine();
            }
        }
    }
}
