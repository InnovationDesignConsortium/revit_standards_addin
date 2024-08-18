using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

namespace RevitDataValidator
{
    public static class Update
    {
        public static string MsiToRunOnExit = null;
        public static readonly string githubToken = "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";
        public const string OWNER = "InnovationDesignConsortium";
        private const string REPO = "revit_standards_addin";

        public static void CheckForUpdates()
        {
            var url = $"https://api.github.com/repos/{OWNER}/{REPO}/releases";
            try
            {
                var stream = Utils.GetPrivateRepoStream(url, githubToken);
                using (var reader = new StreamReader(stream))
                {
                    var releasesJson = reader.ReadToEnd();
                    var releases = JsonConvert.DeserializeObject<List<GithubResponse>>(releasesJson);
                    if (releases == null)
                    {
                        return;
                    }
                    var latestRelease = releases
                        .Where(q => !q.draft)
                        .Where(q => !q.prerelease)
                            .OrderByDescending(release => release.published_at)
                            .FirstOrDefault();
                    if (latestRelease == null || latestRelease.assets.Count == 0)
                    {
                        return;
                    }
                    var thisAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                    var webVersion = new Version(latestRelease.tag_name.Substring(1));
                    bool isWebNewer = webVersion.CompareTo(thisAssemblyVersion) > 0;
                    if (isWebNewer)
                    {
                        var td = new Autodesk.Revit.UI.TaskDialog("Revit Validator Update Found")
                        {
                            CommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons.Yes | Autodesk.Revit.UI.TaskDialogCommonButtons.No,
                            MainInstruction = "An update for the Revit Validator has been found.\nWould you like to install this version after you exit Revit?",
                            MainContent = $"Release Name: {latestRelease.name}{Environment.NewLine}Published at: {latestRelease.published_at}"
                        };
                        if (td.Show() == Autodesk.Revit.UI.TaskDialogResult.Yes)
                        {
                            var asset = latestRelease.assets.First();
                            DownloadAsset(latestRelease.tag_name, asset);
                        }
                        else
                        {
                            MsiToRunOnExit = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogException("Exception checking for updates:", ex);
            }
            return;
        }

        private static void DownloadAsset(string tag, Asset asset)
        {
            try
            {
                var fileName = Path.Combine(Utils.dllPath, asset.name);
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                // https://github.com/gruntwork-io/fetch
                var arguments = $"-repo https://github.com/{OWNER}/{REPO} --tag=\"{tag}\" --release-asset=\"{asset.name}\" --github-oauth-token {githubToken} {Utils.dllPath}";

                Utils.StartShell(
                    $"{Utils.dllPath}\\fetch_windows_amd64.exe", false, arguments);

                MsiToRunOnExit = fileName;
            }
            catch (Exception ex)
            {
                Utils.LogException("Exception downloading update:", ex);
            }
        }
    }
}