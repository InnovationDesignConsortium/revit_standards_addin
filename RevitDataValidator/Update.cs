using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

namespace RevitDataValidator
{
    public static class Update
    {
        public static string MsiToRunOnExit = null;
        private static readonly string githubToken = "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";
        public const string OWNER = "InnovationDesignConsortium";
        private const string REPO = "revit_standards_addin";

        public static async Task CheckForUpdates()
        {
            try
            {
                var request = CreateRequest($"https://api.github.com/repos/{OWNER}/{REPO}/releases");
                using (var response = request.Result)
                {
#if PRE_NET_8
                    using (var reader = new StreamReader(response.Content.ReadAsStreamAsync().Result))
#else
                    using (var reader = new StreamReader(response.Content.ReadAsStream()))
#endif
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
                                await DownloadAsset(latestRelease.tag_name, asset);
                            }
                            else
                            {
                                MsiToRunOnExit = null;
                            }
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

        private static async Task<HttpResponseMessage> CreateRequest(string url)
        {
            try
            {
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    requestMessage.Headers.UserAgent.ParseAdd("test app");
                    requestMessage.Headers.Accept.ParseAdd("application/vnd.github.v3.raw");
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("token", githubToken);
                    var httpClient = new HttpClient();
#if PRE_NET_8
                    return null;
#else
                    return await httpClient.SendAsync(requestMessage);
#endif
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static async Task DownloadAsset(string tag, Asset asset)
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

                //HttpResponseMessage response;
                //using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, asset.url))
                //{
                //    requestMessage.Headers.Accept.ParseAdd("application/octet-stream");
                //    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
                //    requestMessage.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                //    var httpClient = new HttpClient();
                //    response = await httpClient.SendAsync(requestMessage);
                //}

                //var content = response.Content;
                //await content.CopyToAsync(new FileStream(fileName, FileMode.Create));

                MsiToRunOnExit = fileName;
            }
            catch (Exception ex)
            {
                Utils.LogException("Exception downloading update:", ex);
            }
        }
    }
}