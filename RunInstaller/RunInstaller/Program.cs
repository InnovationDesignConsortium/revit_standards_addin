using NLog.Config;
using NLog.Targets;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static void Main(string[] args)
    {
        var filename = Path.Combine(Path.GetTempPath(), "RevitDataValidator.msi");
        if (args.Length > 0)
        {
            filename = args[0];
        }

        var dll = typeof(Program).Assembly.Location;
        var dllPath = Path.GetDirectoryName(dll);
        LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(dllPath, "NLog.config"));
        var logConfig = LogManager.Configuration;
        var targets = logConfig.AllTargets;

        foreach (var target in targets)
        {
            if (target is FileTarget ft)
            {
                ft.FileName = 
                    string.Concat(ft.FileName.ToString()
                        .Substring(0, ft.FileName.ToString().Length - 4),
                        " INSTALL ", 
                        DateTime.Now.ToString().Replace(":", "-")
                            .Replace("/", "_"), ".log");
            }
        }
        LogManager.Configuration = logConfig;
        const string processName = "revit";

        var processes = Process.GetProcessesByName(processName).ToList();

        Logger.Info($"Found {processes.Count} running {processName} processes");

        foreach (var process in processes)
        {
            process.WaitForExit();
            Logger.Info($"Process {process.ProcessName} exited");
        }
        var startInfo = new ProcessStartInfo
        {
            FileName = filename,
            Arguments = "",
            CreateNoWindow = true,
            UseShellExecute = true
        };
        Logger.Info($"About to start {startInfo.FileName}");
        var p = Process.Start(startInfo);
        p?.WaitForExit();
        Logger.Info($"Completed {startInfo.FileName}");
    }
}