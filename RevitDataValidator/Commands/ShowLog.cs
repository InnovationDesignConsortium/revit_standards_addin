using Autodesk.Revit.Attributes;
using NLog;
using NLog.Layouts;
using NLog.Targets;
using System;
using System.Diagnostics;
using System.IO;

namespace RevitDataValidator
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ShowLogCommand : Nice3point.Revit.Toolkit.External.ExternalCommand
    {
        public override void Execute()
        {
            var logConfig = LogManager.Configuration;
            var targets = logConfig.AllTargets;
            try
            {
                foreach (var target in targets)
                {
                    if (target is FileTarget ft && ft.FileName is SimpleLayout layout && layout.IsFixedText)
                    {
                        var filename = layout.FixedText.Replace(@"\/", "/");
                        if (File.Exists(filename))
                        {
                            Process.Start(new ProcessStartInfo(filename)
                            {
                                UseShellExecute = true
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogException("Error opening log", ex);
            }
        }
    }
}