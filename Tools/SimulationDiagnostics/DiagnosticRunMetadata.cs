using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Baseball.Core.Rules;

namespace Baseball.Tools.SimulationDiagnostics
{
    /// <summary>
    /// 진단 결과를 재현하는 데 필요한 소스·런타임·규칙 메타데이터를 출력한다.
    /// </summary>
    internal static class DiagnosticRunMetadata
    {
        public static void Write(string command, int sampleCount, string outputProfile)
        {
            SimulationVersionStamp version = SimulationVersionStamp.CreateCurrent(balanceVersion: 0);
            string repositoryRoot = FindRepositoryRoot();
            Console.WriteLine($"Command={command}");
            Console.WriteLine($"Samples={sampleCount:N0}");
            Console.WriteLine($"GitCommit={ReadGit(repositoryRoot, "rev-parse HEAD")}");
            Console.WriteLine($"WorkingTreeDirty={ReadDirtyState(repositoryRoot)}");
            Console.WriteLine($"Runtime={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"Architecture={RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($"CPU={Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown"}");
#if DEBUG
            Console.WriteLine("BuildConfiguration=Debug");
#else
            Console.WriteLine("BuildConfiguration=Release");
#endif
            Console.WriteLine($"BalanceVersion={version.BalanceVersion}");
            Console.WriteLine($"EngineVersion={version.EngineVersion}");
            Console.WriteLine($"RulesVersion={version.RulesVersion}");
            Console.WriteLine($"RngVersion={version.RngAlgorithmVersion}");
            Console.WriteLine($"ContentHash={version.ContentHash}");
            Console.WriteLine("EngineKind=Detailed");
            Console.WriteLine($"OutputProfile={outputProfile}");
            Console.WriteLine("SeedBase=0xD37A11ED");
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            return Directory.GetCurrentDirectory();
        }

        private static string ReadDirtyState(string repositoryRoot)
        {
            string status = ReadGit(repositoryRoot, "status --porcelain");
            return status == "unknown" ? status : (!string.IsNullOrEmpty(status)).ToString();
        }

        private static string ReadGit(string repositoryRoot, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    WorkingDirectory = repositoryRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add($"safe.directory={repositoryRoot.Replace('\\', '/')}");
                string[] parts = arguments.Split(' ');
                for (int index = 0; index < parts.Length; index++)
                    startInfo.ArgumentList.Add(parts[index]);

                using Process process = Process.Start(startInfo);
                if (process == null)
                    return "unknown";
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
