using System;
using System.Diagnostics;
using System.Threading;

namespace RecoilStarter
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var backgroundProcessing = false; // set to lower the IO priority automatically

            // NVMe (PCIe 4.0 x4) SSD
            var gameDir = "C:\\temp_storage_test\\Genshin Impact Game";
            var pipeFatness = 4194304; // 8000MiB/s * 500μs
            var randomAccessPreference = 256;

            // NVMe (PCIe 3.0 x4) SSD
            //var gameDir = "D:\\Program Files\\Genshin Impact\\Genshin Impact Game";
            //var pipeFatness = 1835008; // 3500MiB/s * 500μs
            //var randomAccessPreference = 256;

            // SATA SSD
            // (TBD)

            // SATA HDD
            // (TBD)

            // NAS with ZFS RAIDZ2 HDD, over 10Gbps local network
            //var gameDir = "\\\\li-nas01\\public\\dropbox\\temp_storage_test\\Genshin Impact Game";
            //var pipeFatness = 550502; // 525MiB/s * 1ms
            //var randomAccessPreference = 256;

            if (backgroundProcessing)
            {
                // set CPU and IO priority to low so that we don't disturb other programs during hashing
                using (Process p = Process.GetCurrentProcess()) p.PriorityClass = ProcessPriorityClass.BelowNormal;
                var ioPrio = (int)IOPriority.Low;
                Win32.NtSetInformationProcess(-1, PROCESS_INFORMATION_CLASS.ProcessIoPriority, ref ioPrio, 4);
            }

            var hasher = new ManagedFileHasher(gameDir, pipeFatness, randomAccessPreference);
            Console.Error.WriteLine("[*] Starting hash threads...");
            var watch = Stopwatch.StartNew();
            hasher.Run();
            watch.Stop();
            Console.Error.WriteLine("[+] Hasher returned.");

            if (backgroundProcessing)
            {
                // reset CPU and IO priority
                using (Process p = Process.GetCurrentProcess()) p.PriorityClass = ProcessPriorityClass.Normal;
                var ioPrio = (int)IOPriority.Normal;
                Win32.NtSetInformationProcess(-1, PROCESS_INFORMATION_CLASS.ProcessIoPriority, ref ioPrio, 4);
            }

            // print system info
            // Note: for .net Framework 4.x, Environment.Version always return 4.0.xxxxx.yyyyy;
            // https://learn.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
            ThreadPool.GetMaxThreads(out int workerThreadsCount, out int ioThreadsCount);
            Console.Error.WriteLine(string.Format("[i] OS: {0}", Environment.OSVersion.ToString()));
            Console.Error.WriteLine(string.Format("[i] CLR: {0}, Workers: {1}, IO threads: {2}", Environment.Version.ToString(), workerThreadsCount, ioThreadsCount));

            // print performance statistics
            Console.Error.WriteLine(string.Format("[i] Directory: {0}", gameDir));
            Console.Error.WriteLine(string.Format("[i] Total files: {0}, Total size: {1:0.00}MiB", hasher.FileCount, (double)hasher.FileSize / 1048576));
            Console.Error.WriteLine(string.Format("[i] Time: {0:0.00}s", (double)watch.ElapsedMilliseconds / 1000));
            Console.Error.WriteLine(string.Format("[i] Speed: {0:0.00}files/s, {1:0.00}MiB/s", (double)hasher.FileCount / watch.ElapsedMilliseconds * 1000, (double)hasher.FileSize / watch.ElapsedMilliseconds * 1000 / 1048576));

            hasher.Dispose();

            if (Debugger.IsAttached)
            {
                Console.Error.WriteLine("[i] Press <Enter> to exit...");
                Console.ReadLine();
            }
        }
    }
}
