using NLog.Config;
using NLog.Targets;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;

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
        if (dllPath == null)
        {
            return;
        }
        LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(dllPath, "NLog.config"));
        var logConfig = LogManager.Configuration;
        var targets = logConfig.AllTargets;
        const string INSTALLER_NAME = "RevitValidatorInstaller";
        foreach (var target in targets)
        {
            if (target is FileTarget ft)
            {
                ft.FileName = "${tempdir}/" + INSTALLER_NAME + DateTime.Now.ToString().Replace(":", "-").Replace("/", "_") + ".log";
            }
        }
        LogManager.Configuration = logConfig;
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

        //var versions = new List<string> { "2023", "2024", "2025", "2026", "2027" };
        //var dir = Path.GetDirectoryName(dllPath);
        //var addinsFolder = Path.GetDirectoryName(dir);
        //var revitversion = new DirectoryInfo(dir).Name;

        //foreach (var version in versions)
        //{
        //    dir = dllPath.Replace(revitversion, version);
        //    if (Directory.Exists(dir))
        //    {
        //        foreach (var file in Directory.GetFiles(dir))
        //        {
        //            try
        //            {
        //                File.Delete(file);
        //                Logger.Info("Deleted " + file);
        //            }
        //            catch { }
        //        }
        //    }
        //    var addinsFolderWithVersion = Path.Combine(addinsFolder, version);
        //    var addinFile = Path.Combine(addinsFolderWithVersion, "RevitDataValidator.addin");
        //    if (File.Exists(addinFile))
        //    {
        //        try
        //        {
        //            File.Delete(addinFile);
        //            Logger.Info($"Deleted {addinFile}");
        //        }
        //        catch { }
        //    }
        //}

        if (File.Exists(filename))
        {
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
        else
        {
            Logger.Info(filename + " does not exist");
        }
    }
}