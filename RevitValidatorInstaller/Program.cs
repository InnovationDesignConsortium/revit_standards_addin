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
        const string INSTALLER_NAME = "RevitValidatorInstaller";
        var currentProcess = Process.GetCurrentProcess();

        var otherInstallerProcesses = Process.GetProcessesByName(INSTALLER_NAME)
            .ToList().Where(q => q.Id != currentProcess.Id).ToList();

        if (otherInstallerProcesses.Count > 0)
        {
            Logger.Info($"Exiting because {otherInstallerProcesses.Count} running {INSTALLER_NAME} processes found (ids {string.Join(",", otherInstallerProcesses.Select(q => q.Id))})");
            return;
        }

        const string REVIT_PROCESS_NAME = "revit";

        var revitProcesses = Process.GetProcessesByName(REVIT_PROCESS_NAME).ToList();
        if (revitProcesses?.Count > 0)
        {
            Logger.Info($"Found {revitProcesses.Count} running {REVIT_PROCESS_NAME} processes with ids {string.Join(",", revitProcesses.Select(q => q.Id))}");

            foreach (var process in revitProcesses)
            {
                process.WaitForExit();
                Logger.Info($"Process {process.ProcessName} {process.Id} exited");
            }
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