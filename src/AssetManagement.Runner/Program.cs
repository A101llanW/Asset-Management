using System;
using System.Diagnostics;
using System.IO;

namespace AssetManagement.Runner
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var port = args.Length > 0 ? args[0] : "51901";
            var webPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "AssetManagement.Web"));
            var iisExpressPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express", "iisexpress.exe");
            if (!File.Exists(iisExpressPath))
            {
                Console.Error.WriteLine("IIS Express not found. Install IIS Express to run the web module.");
                Environment.Exit(1);
                return;
            }

            if (!Directory.Exists(webPath))
            {
                Console.Error.WriteLine("Web project path not found: " + webPath);
                Environment.Exit(1);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = iisExpressPath,
                Arguments = "/path:\"" + webPath + "\" /port:" + port,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start IIS Express.");
                Environment.Exit(1);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:" + port + "/",
                UseShellExecute = true
            });

            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.Error.WriteLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Console.WriteLine("AssetManagement web app running on http://localhost:" + port + "/");
            Console.WriteLine("Press ENTER to stop.");
            Console.ReadLine();

            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit();
            }
        }
    }
}
