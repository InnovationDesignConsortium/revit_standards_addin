using System.Diagnostics;

foreach (Process process in Process.GetProcessesByName("revit"))
{
    process.WaitForExit();
}
var startInfo = new ProcessStartInfo
{
    FileName = args[0],
    Arguments = "",
    CreateNoWindow = true,
    UseShellExecute = false
};
Process.Start(startInfo);