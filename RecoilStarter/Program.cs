using System;
using System.Diagnostics;

namespace RecoilStarter
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // system info
            // Note: for .net Framework 4.x, this value always return 4.0.xxxxx.yyyyy;
            // https://learn.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed
            Console.WriteLine("CLR Version: {0}", Environment.Version.ToString());

            string game_dir = "D:\\Program Files\\Genshin Impact\\Genshin Impact Game";
            ManagedFileHasher hasher = new ManagedFileHasher(game_dir, "SSD_NVME");
            Console.WriteLine("[*] Starting hash threads...");
            var watch = Stopwatch.StartNew();
            hasher.Run();
            watch.Stop();
            Console.WriteLine("[+] Hasher returned.");
            Console.WriteLine(String.Format("[i] Time: {0}ms", watch.ElapsedMilliseconds));

            if (Debugger.IsAttached)
            {
                Console.WriteLine("[i] Press <Enter> to exit...");
                Console.ReadLine();
            }
        }
    }
}
