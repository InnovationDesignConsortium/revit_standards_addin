using System;
using System.Linq;

namespace RevitDataValidator
{
    public static class Update
    {
        public static void CheckForUpdates(bool updateWithoutPrompt = false)
        {
            try
            {
                var latestRelease = Utils.GetLatestWebRelase();
                if (latestRelease == null)
                {
                    return;
                }
                var webVersion = new Version(latestRelease.tag_name.Substring(1));
                if (Utils.IsWebVersionNewer(webVersion))
                {
                    var td = new Autodesk.Revit.UI.TaskDialog($"{Utils.PRODUCT_NAME} Update Found")
                    {
                        TitleAutoPrefix = false,
                        CommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons.Yes | Autodesk.Revit.UI.TaskDialogCommonButtons.No,
                        MainInstruction = $"An update for the {Utils.PRODUCT_NAME} has been found. Would you like to install this version after you exit Revit?",
                        MainContent = $"Installed Version: {Utils.GetInstalledVersion()}{Environment.NewLine}Newest Released Version: {webVersion}{Environment.NewLine}Release Date of Newest Version: {latestRelease.published_at}"
                    };
                    if (updateWithoutPrompt ||
                        td.Show() == Autodesk.Revit.UI.TaskDialogResult.Yes)
                    {
                        var asset = latestRelease.assets.First();
                        Utils.Log($"Chose to install update to {webVersion}", LogLevel.Info);
                        Utils.DownloadAsset(latestRelease.tag_name, asset);
                    }
                    else
                    {
                        Utils.Log($"Chose to NOT install update to {webVersion}", LogLevel.Info);
                        Utils.MsiToRunOnExit = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogException("Exception checking for updates:", ex);
            }
            return;
        }
    }
}