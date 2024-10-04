using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;
using Octokit;
using Revit.Async;
using RevitDataValidator.Classes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace RevitDataValidator
{
    internal class Ribbon : Nice3point.Revit.Toolkit.External.ExternalApplication
    {
        private const string RULE_FILE_NAME = "rules.md";
        private readonly string PARAMETER_PACK_FILE_NAME = "parameterpacks.json";
        private readonly string RULE_DEFAULT_MESSAGE = "This is not allowed. (A default error message is given because the rule registered after Revit startup)";
        private FailureDefinitionId genericFailureId;
        public static UpdaterId DataValidationUpdaterId;
        public static string gitRuleFilePath;

        private const string SERVER_ENV = "RevitStandardsAddinGitServerUrl";
        private const string OWNER_ENV = "RevitStandardsAddinGitOwner";
        private const string REPO_ENV = "RevitStandardsAddinGitRepo";
        private const string PAT_ENV = "RevitStandardsAddinGitPat";

        public override void OnStartup()
        {
            RevitTask.Initialize(Application);
            RevitTask.RegisterGlobal(new CustomRuleExternalEventHandler());

            var dll = typeof(Ribbon).Assembly.Location;
            Utils.dllPath = Path.GetDirectoryName(dll);

            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(Utils.dllPath, "NLog.config"));
            var logConfig = LogManager.Configuration;
            var targets = logConfig.AllTargets;
            foreach (var target in targets)
            {
                if (target is FileTarget ft)
                {
                    ft.FileName = string.Concat(ft.FileName.ToString().AsSpan(0, ft.FileName.ToString().Length - 4), " ", DateTime.Now.ToString().Replace(":", "-").Replace("/", "_"), ".log");
                }
            }
            LogManager.Configuration = logConfig;

            Utils.Log($"Running version: {Utils.GetInstalledVersion()}", LogLevel.Trace);

            GetEnvironmentVariableData();

            Utils.token_for_GIT_CODE_REPO_OWNER = GetGithubTokenFromApp(Utils.GIT_CODE_REPO_OWNER);

            Utils.dictCategoryPackSet = new Dictionary<string, string>();
            Utils.dictCustomCode = new Dictionary<string, Type>();
            Utils.app = Application.ControlledApplication;
            Utils.allParameterRules = new List<ParameterRule>();
            Utils.allWorksetRules = new List<WorksetRule>();
            Application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;
            Application.ControlledApplication.DocumentSaving += ControlledApplication_DocumentSaving;
            Application.ControlledApplication.DocumentSynchronizingWithCentral += ControlledApplication_DocumentSynchronizingWithCentral;
            Application.ViewActivated += Application_ViewActivated;
            Application.DialogBoxShowing += Application_DialogBoxShowing;
            Application.ControlledApplication.DocumentClosed += ControlledApplication_DocumentClosed;
            Application.Idling += Application_Idling;
            Utils.eventHandlerWithParameterObject = new EventHandlerWithParameterObject();
            Utils.eventHandlerCreateInstancesInRoom = new EventHandlerCreateInstancesInRoom();

            Utils.paneId = new DockablePaneId(Guid.NewGuid());
            Utils.propertiesPanel = new PropertiesPanel();

            Application.ControlledApplication.DocumentChanged += ControlledApplication_DocumentChanged;

            Application.RegisterDockablePane(Utils.paneId, "Properties Panel", Utils.propertiesPanel as IDockablePaneProvider);
            Application.SelectionChanged += Application_SelectionChanged;

            foreach (BuiltInCategory bic in Enum.GetValues(typeof(BuiltInCategory)))
            {
                if (bic.ToString().Contains("_gbXML_"))
                {
                    continue;
                }
                try
                {
                    Utils.catMap.Add(LabelUtils.GetLabelFor(bic), bic);
                }
                catch
                { }
            }

            DataValidationUpdater dataValidationUpdater = new DataValidationUpdater(Application.ActiveAddInId);
            DataValidationUpdaterId = dataValidationUpdater.GetUpdaterId();
            UpdaterRegistry.RegisterUpdater(dataValidationUpdater, true);

            genericFailureId = new FailureDefinitionId(Guid.NewGuid());
            FailureDefinition.CreateFailureDefinition(
                genericFailureId,
                FailureSeverity.Error,
                RULE_DEFAULT_MESSAGE);

            var panel = Application.GetRibbonPanels().Find(q => q.Name == Utils.panelName) ?? Application.CreateRibbonPanel(Utils.panelName);

            var showPaneCommand = new PushButtonData("ShowPaneCommand", "Show\nPane", dll, "RevitDataValidator.ShowPaneCommand")
            {
                Image = NewBitmapImage(GetType().Namespace, "show16.png"),
                LargeImage = NewBitmapImage(GetType().Namespace, "show.png")
            };
            panel.AddItem(showPaneCommand);

            var aboutCommand = new PushButtonData("AboutCommand", "About", dll, "RevitDataValidator.AboutCommand")
            {
                Image = NewBitmapImage(GetType().Namespace, "about16.png"),
                LargeImage = NewBitmapImage(GetType().Namespace, "about.png")
            };
            panel.AddItem(aboutCommand);

            Update.CheckForUpdates();
        }

        public static BitmapImage NewBitmapImage(string ns, string imageName)
        {
            string imagePath = ns + ".ImageFiles." + imageName;
            Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(imagePath);
            BitmapImage img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = s;
            img.EndInit();
            return img;
        }

        private void ControlledApplication_DocumentSynchronizingWithCentral(object sender, Autodesk.Revit.DB.Events.DocumentSynchronizingWithCentralEventArgs e)
        {
            Utils.RunAllRules(null, WhenToRun.SyncToCentral);
        }

        private void ControlledApplication_DocumentSaving(object sender, Autodesk.Revit.DB.Events.DocumentSavingEventArgs e)
        {
            Utils.RunAllRules(null, WhenToRun.Save);
        }

        public override void OnShutdown()
        {
            if (Utils.MsiToRunOnExit != null)
            {
                Utils.Log($"Installing new version {Utils.MsiToRunOnExit}", LogLevel.Trace);
                try
                {
                    Utils.StartShell(Utils.MsiToRunOnExit, true);
                }
                catch (Exception ex)
                {
                    Utils.LogException("Could not install new version", ex);
                }
            }
        }

        private static void GetEnvironmentVariableData()
        {
            if (Environment.GetEnvironmentVariable("RevitDataValidatorDebug", EnvironmentVariableTarget.Machine) == "1")

            {
                Utils.Debugging = true;
            }
            else
            {
                Utils.Debugging = false;
            }

            Utils.GIT_ENTERPRISE_SERVER_URL = Environment.GetEnvironmentVariable(SERVER_ENV, EnvironmentVariableTarget.Machine);
            if (Utils.GIT_ENTERPRISE_SERVER_URL != null)
            {
                Utils.Log($"SERVER_ENV = {SERVER_ENV}", LogLevel.Trace);
            }

            Utils.GIT_OWNER = Environment.GetEnvironmentVariable(OWNER_ENV, EnvironmentVariableTarget.Machine);
            if (Utils.GIT_OWNER == null)
            {
                Utils.Log($"Environment variable {OWNER_ENV} is empty", LogLevel.Error);
            }
            else
            {
                Utils.Log($"OWNER_ENV = {Utils.GIT_OWNER}", LogLevel.Trace);
            }

            Utils.GIT_REPO = Environment.GetEnvironmentVariable(REPO_ENV, EnvironmentVariableTarget.Machine);
            if (Utils.GIT_REPO == null)
            {
                Utils.Log($"Environment variable {REPO_ENV} is empty", LogLevel.Error);
            }
            else
            {
                Utils.Log($"REPO_ENV = {Utils.GIT_REPO}", LogLevel.Trace);
            }

            var git_pat = Environment.GetEnvironmentVariable(PAT_ENV, EnvironmentVariableTarget.Machine);
            if (string.IsNullOrEmpty(git_pat))
            {
                Utils.tokenFromGithubApp = GetGithubTokenFromApp(Utils.GIT_OWNER);
            }
            else
            {
                Utils.tokenFromGithubApp = new TokenInfo { token = git_pat };
                Utils.Log($"Github: Using personal access token {git_pat}", LogLevel.Trace);
            }
        }

        private static TokenInfo GetGithubTokenFromApp(string owner)
        {
            // https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/authenticating-as-a-github-app-installation

            // 1 - Generate a JSON web token (JWT) for your app

            var jsonWebToken = GenerateJwtToken();
            if (string.IsNullOrEmpty(jsonWebToken))
            {
                Utils.Log("JwtToken is empty", LogLevel.Error);
                return null;
            }

            // 2 - Get the ID of the installation that you want to authenticate as
            var installationResponse = Utils.GetRepoData("https://api.github.com/app/installations", HttpMethod.Get, jsonWebToken, "application/vnd.github+json", "Bearer");
            var installations = ((JArray)JsonConvert.DeserializeObject(installationResponse)).ToObject<List<GitHubAppInstallation>>();
            var installation = installations?.FirstOrDefault(q => q.account.login == owner);
            if (installation == null)
            {
                var td = new TaskDialog("Error")
                {
                    MainInstruction = $"Github app must be installed for {owner}",
                    MainContent = "<a href=\"https://github.com/apps/revitstandardsgithubapp/installations/new\">https://github.com/apps/revitstandardsgithubapp/installations/new</a>"
                };
                td.Show();

                Utils.Log($"Installation does not exist for {owner}", LogLevel.Error);
                return null;
            }
            var instalationId = installation?.id;

            // 3 - Send a REST API POST request to /app/installations/INSTALLATION_ID/access_tokens
            var accessTokenResponse = Utils.GetRepoData($"https://api.github.com/app/installations/{instalationId}/access_tokens", HttpMethod.Post, jsonWebToken, "application/vnd.github+json", "Bearer");
            var tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(accessTokenResponse);
            Utils.Log($"Github: content permissions = {tokenInfo.permissions.contents}", LogLevel.Trace);
            return tokenInfo;
        }

        private static string GenerateJwtToken()
        {
            try
            {
                var pathtoexe = Path.Combine(Utils.dllPath, "CreateJsonWebToken", "CreateJsonWebToken.exe");
                if (File.Exists(pathtoexe))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = pathtoexe,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    };
                    var pp = Process.Start(startInfo);
                    var output = pp.StandardOutput.ReadToEnd();
                    pp.WaitForExit();
                    return output;
                }
                else
                {
                    return "";
                }
            }
            catch (Exception ex)
            {
                Utils.LogException("Failed to generate JwtToken", ex);
                return null;
            }
        }

        private void ControlledApplication_DocumentClosed(object sender, Autodesk.Revit.DB.Events.DocumentClosedEventArgs e)
        {
            Utils.doc = null;
        }

        private void ControlledApplication_DocumentChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
        {
            Utils.propertiesPanel?.Refresh();
        }

        private void Application_Idling(object sender, IdlingEventArgs e)
        {
            Utils.dialogIdShowing = "";
            Utils.CustomCodeRunning = new List<string>();
            Utils.idsTriggered = new List<ElementId>();
        }

        private void Application_DialogBoxShowing(object sender, DialogBoxShowingEventArgs e)
        {
            Utils.dialogIdShowing = e.DialogId;
        }

        private void Application_ViewActivated(object sender, ViewActivatedEventArgs e)
        {
            var currentFilename = Utils.GetFileName(e.Document);
            if (Utils.doc == null || currentFilename != Utils.doc.PathName)
            {
                Utils.doc = e.Document;
                var newFilename = Utils.GetFileName(e.Document);
                Utils.userName = e.Document.Application.Username;
                Utils.allParameterRules.Clear();
                Utils.allWorksetRules.Clear();

                if (Utils.doc != null)
                {
                    try
                    {
                        UpdaterRegistry.RemoveDocumentTriggers(DataValidationUpdaterId, Utils.doc);
                    }
                    catch
                    {
                    }
                }

                string parameterPackFileContents = null;
                if (Utils.parameterPackDatas.TryGetValue(newFilename, out RuleFileInfo cachedParameterFileInfo))
                {
                    parameterPackFileContents = cachedParameterFileInfo.Contents;
                }
                else
                {
                    var parameterPackFilePath = GetGitFileNamesFromConfig();
                    if (parameterPackFilePath == null)
                    {
                        return;
                    }

                    var file = Path.Combine(Utils.dllPath, PARAMETER_PACK_FILE_NAME);
                    if (Utils.Debugging && File.Exists(file))
                    {
                        parameterPackFileContents = File.ReadAllText(file);
                        Utils.Log($"Read parameter packs from {file}", LogLevel.Info);
                    }
                    else
                    {
                        var data = Utils.GetGitData(ContentType.File, $"{parameterPackFilePath}/{PARAMETER_PACK_FILE_NAME}");
                        if (data == null)
                        {
                            Utils.Log($"No parameter pack data at {parameterPackFilePath}", LogLevel.Warn);
                        }
                        else
                        {
                            Utils.Log($"Found parameter pack file {parameterPackFilePath}", LogLevel.Trace);
                            parameterPackFileContents = data.Content;
                        }
                    }
                }
                if (parameterPackFileContents == null)
                {
                    Utils.parameterUIData = new ParameterUIData();
                }
                else
                {
                    Utils.parameterUIData = JsonConvert.DeserializeObject<ParameterUIData>(parameterPackFileContents, new JsonSerializerSettings
                    {
                        Error = HandleDeserializationError,
                        MissingMemberHandling = MissingMemberHandling.Error
                    });
                }

                var ruleFileInfo = new RuleFileInfo();
                string ruleFileContents = null;
                if (Utils.ruleDatas.TryGetValue(newFilename, out RuleFileInfo cachedRuleFileInfo))
                {
                    ruleFileContents = cachedRuleFileInfo.Contents;
                }
                else
                {
                    var ruleFile = Directory.GetFiles(Utils.dllPath).FirstOrDefault(q => Path.GetFileName(q) == RULE_FILE_NAME);
                    if (Utils.Debugging && ruleFile != null)
                    {
                        using (var reader = new StreamReader(new FileStream(ruleFile, System.IO.FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                        {
                            ruleFileContents = reader.ReadToEnd();
                        }
                        ruleFileInfo.Filename = ruleFile;
                        ruleFileInfo.FilePath = Path.GetDirectoryName(ruleFile);
                    }
                    else
                    {
                        gitRuleFilePath = GetGitFileNamesFromConfig();
                        if (gitRuleFilePath == null)
                        {
                            return;
                        }
                        RepositoryContent ruleData = null;
                        if (gitRuleFilePath != null)
                        {
                            ruleData = Utils.GetGitData(ContentType.File, $"{gitRuleFilePath}/{RULE_FILE_NAME}");
                            ruleFileInfo.Url = ruleData.HtmlUrl;
                            ruleFileContents = ruleData.Content;
                            ruleFileInfo.FilePath = gitRuleFilePath;
                        }
                    }

                    if (ruleFileContents == null)
                    {
                        Utils.ruleDatas.Add(newFilename, new RuleFileInfo());
                        Utils.propertiesPanel.Refresh();
                        return;
                    }
                    else
                    {
                        ruleFileInfo.Contents = ruleFileContents;
                        Utils.ruleDatas.Add(newFilename, ruleFileInfo);
                    }
                }

                var parameterRules = new List<ParameterRule>();
                var worksetRules = new List<WorksetRule>();

                MarkdownDocument document = Markdown.Parse(ruleFileContents);
                var descendents = document.Descendants();
                var codeblocks = document.Descendants<FencedCodeBlock>().ToList();
                foreach (var block in codeblocks)
                {
                    var lines = block.Lines.Cast<StringLine>().Select(q => q.ToString()).ToList();
                    var json = string.Concat(lines.Where(q => !q.StartsWith("//")).ToList());
                    RuleData rules = null;
                    try
                    {
                        rules = JsonConvert.DeserializeObject<RuleData>(json, new JsonSerializerSettings
                        {
                            Error = HandleDeserializationError,
                            MissingMemberHandling = MissingMemberHandling.Error
                        });
                    }
                    catch (Exception ex)
                    {
                        Utils.LogException("JsonConvert.DeserializeObject", ex);
                    }
                    if (rules != null)
                    {
                        parameterRules = rules.ParameterRules;
                        worksetRules = rules.WorksetRules;

                        if (parameterRules != null)
                        {
                            foreach (var rule in parameterRules)
                            {
                                rule.Guid = Guid.NewGuid();
                            }
                        }
                        if (worksetRules != null)
                        {
                            foreach (var rule in worksetRules)
                            {
                                rule.Guid = Guid.NewGuid();
                            }
                        }
                    }
                }

                if (parameterRules != null)
                {
                    foreach (var parameterRule in parameterRules)
                    {
                        ParameterRule conflictingRule = null;

                        foreach (var existingRule in Utils.allParameterRules)
                        {
                            if (DoParameterRulesConflict(parameterRule, existingRule))
                            {
                                conflictingRule = existingRule;
                                break;
                            }
                        }
                        if (conflictingRule == null)
                        {
                            RegisterParameterRule(parameterRule, ruleFileInfo);
                            Utils.allParameterRules.Add(parameterRule);
                        }
                        else
                        {
                            Utils.Log($"Ignoring parameter rule '{parameterRule}' because it conflicts with the rule '{conflictingRule}'", LogLevel.Error);
                        }
                    }
                }
                if (worksetRules != null)
                {
                    foreach (var worksetRule in worksetRules)
                    {
                        WorksetRule conflictingRule = null;

                        foreach (var existingRule in Utils.allWorksetRules)
                        {
                            if (DoWorksetRulesConflict(worksetRule, existingRule))
                            {
                                conflictingRule = existingRule;
                                break;
                            }
                        }

                        if (conflictingRule == null)
                        {
                            RegisterWorksetRule(worksetRule);
                            Utils.allWorksetRules.Add(worksetRule);
                        }
                        else
                        {
                            Utils.Log($"Ignoring workset rule '{worksetRule}' because it conflicts with the rule '{conflictingRule}'", LogLevel.Error);
                        }
                    }
                }
                Utils.propertiesPanel.Refresh();
                SetupPane();
            }
        }

        private static bool DoParameterRulesConflict(ParameterRule r1, ParameterRule r2)
        {
            if (r1.ParameterName != r2.ParameterName)
            {
                return false;
            }
            if (r1.CustomCode != null || r2.CustomCode != null)
            {
                return false;
            }
            if ((r1.Categories != null && r1.Categories.Intersect(r2.Categories).Any()) ||
                (r1.ElementClasses != null && r1.ElementClasses.Intersect(r2.ElementClasses).Any()))
            {
                return true;
            }
            return false;
        }

        private static bool DoWorksetRulesConflict(WorksetRule r1, WorksetRule other)
        {
            if (r1.Workset == other.Workset)
            {
                return false;
            }
            if (!r1.Categories.Intersect(other.Categories).Any())
            {
                return false;
            }
            if (r1.Parameters.Intersect(other.Parameters).Count() == r1.Parameters.Count)
            {
                return true;
            }
            return false;
        }

        private static string GetGitFileNamesFromConfig()
        {
            var projectName = Utils.GetFileName();

            var path = "Standards/RevitStandardsPanel/Config.json";
            var data = Utils.GetGitData(ContentType.File, path);

            if (data == null)
            {
                Utils.Log($"No git data at {path}", LogLevel.Warn);
                return null;
            }

            var json = data.Content;
            var configs = JsonConvert.DeserializeObject<GitRuleConfigRoot>(json, new JsonSerializerSettings
            {
                Error = HandleDeserializationError,
                MissingMemberHandling = MissingMemberHandling.Error
            });

            foreach (var config in configs.StandardsConfig)
            {
                foreach (var regex in config.RvtFullPathRegex)
                {
                    try
                    {
                        var matches = Regex.Matches(projectName, regex);
                        if (matches?.Count > 0)
                        {
                            return config.PathToStandardsFiles;
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.LogException("Regex failed", ex);
                    }
                }
            }
            return null;
        }

        private static void SetupPane()
        {
            var doc = Utils.doc;
            if (doc == null)
                return;

            Utils.doc = doc;
            var app = doc.Application;
            var uiapp = new UIApplication(app);
            var pane = uiapp.GetDockablePane(Utils.paneId);

            Utils.propertiesPanel.SaveTextBoxValues();

            Element element = null;
            if (Utils.selectedIds == null || Utils.selectedIds.Count == 0)
            {
                element = doc.ActiveView;
            }
            else
            {
                element = doc.GetElement(Utils.selectedIds[0]);
            }

            if (element?.Category == null)
                return;

            var catName = element.Category.Name;

            if (Utils.parameterUIData == null)
            {
                return;
            }

            if (Utils.parameterUIData.PackSets != null)
            {
                var validPacks = Utils.parameterUIData.PackSets.Where(q => q.Category == catName).ToList();
                if (validPacks.Count == 0)
                {
                    Utils.propertiesPanel.Refresh(null);
                }
                else if (Utils.dictFileActivePackSet.TryGetValue(Utils.GetFileName(), out string selectedPackSet) &&
                    validPacks.Find(q => q.Name == selectedPackSet) != null)
                {
                    Utils.propertiesPanel.cboParameterPack.SelectedItem = selectedPackSet;
                    Utils.propertiesPanel.Refresh(selectedPackSet);
                }
                else if (validPacks?.Count > 0)
                {
                    PackSet packSet = null;
                    if (Utils.dictCategoryPackSet.TryGetValue(catName, out string value))
                    {
                        packSet = validPacks.Find(q => q.Name == value);
                    }
                    if (packSet == null)
                    {
                        packSet = validPacks[0];
                    }

                    if (packSet != null)
                    {
                        var packSetName = packSet.Name;
                        Utils.propertiesPanel.cboParameterPack.SelectedItem = packSetName;
                        Utils.propertiesPanel.Refresh(packSetName);
                    }
                }
            }
        }

        private void Application_SelectionChanged(object sender, Autodesk.Revit.UI.Events.SelectionChangedEventArgs e)
        {
            Utils.selectedIds = e.GetSelectedElements().ToList();
            SetupPane();
        }

        private void ControlledApplication_DocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
        {
            Utils.doc = e.Document;
            Utils.RunAllRules(null, WhenToRun.Open);
        }

        private static void RegisterWorksetRule(WorksetRule worksetRule)
        {
            Utils.Log("Registering workset rule " + worksetRule, LogLevel.Trace);
            if (worksetRule.Categories != null)
            {
                var builtInCats = Utils.GetBuiltInCats(worksetRule);
                var filter = new LogicalAndFilter(
                    new List<ElementFilter>
                    {
                        new ElementMulticategoryFilter(builtInCats),
                        new ElementIsElementTypeFilter(true)
                    });
                UpdaterRegistry.AddTrigger(
                    DataValidationUpdaterId,
                    Utils.doc,
                    filter,
                    Element.GetChangeTypeAny());
                UpdaterRegistry.AddTrigger(
                    DataValidationUpdaterId,
                    Utils.doc,
                    filter,
                    Element.GetChangeTypeElementAddition());
            }
        }

        private void RegisterParameterRule(ParameterRule rule, RuleFileInfo ruleFileInfo)
        {
            Utils.Log($"Registering parameter rule '{rule}'", LogLevel.Trace);
            try
            {
                if (rule.CustomCode != null)
                {
                    string code = null;
                    var localPath = Path.Combine(Utils.dllPath, $"{rule.CustomCode}.cs");
                    if (Utils.Debugging && File.Exists(localPath))
                    {
                        code = File.ReadAllText(localPath);
                    }
                    if (code == null)
                    {
                        var customCodeFile = $"{gitRuleFilePath}\\{rule.CustomCode}.cs";
                        var repositoryContent = Utils.GetGitData(ContentType.File, customCodeFile);
                        if (repositoryContent == null)
                        {
                            Utils.Log($"Could not get content from file {customCodeFile}", LogLevel.Error);
                            return;
                        }
                        else
                        {
                            code = repositoryContent.Content;
                        }
                    }
                    if (code != null)
                    {
                        var service = new ValidationService();
                        var result = service.Execute(code, out MemoryStream ms);
                        if (result == null)
                        {
                            ms.Seek(0, SeekOrigin.Begin);
                            Assembly assembly = Assembly.Load(ms.ToArray());
                            var type = assembly.GetType(rule.CustomCode);
                            Utils.dictCustomCode[rule.CustomCode] = type;
                        }
                        else
                        {
                            foreach (var error in result)
                            {
                                Utils.Log($"{rule.CustomCode} compilation error: {error.GetMessage()}", LogLevel.Error);
                            }
                        }
                    }
                }
                if (rule.Categories != null)
                {
                    var builtInCats = Utils.GetBuiltInCats(rule);
                    UpdaterRegistry.AddTrigger(
                        DataValidationUpdaterId,
                        Utils.doc,
                        new ElementMulticategoryFilter(builtInCats),
                        Element.GetChangeTypeAny());
                }
                else if (rule.ElementClasses != null)
                {
                    var types = Utils.GetRuleTypes(rule);
                    if (types.Count > 0)
                    {
                        UpdaterRegistry.AddTrigger(
                            DataValidationUpdaterId,
                            Utils.doc,
                            new ElementMulticlassFilter(types),
                            Element.GetChangeTypeAny());
                        UpdaterRegistry.AddTrigger(
                            DataValidationUpdaterId,
                            Utils.doc,
                            new ElementMulticlassFilter(types),
                            Element.GetChangeTypeElementAddition());
                    }
                }

                if (rule.KeyValuePath != null)
                {
                    if (rule.KeyValues != null)
                    {
                        Utils.Log($"Rule should not have both KeyValuePath {rule.KeyValuePath} and KeyValues {rule.KeyValues}", LogLevel.Error);
                    }

                    var fileContents = GetFileContents(rule.KeyValuePath, ruleFileInfo.FilePath);

                    if (fileContents != null)
                    {
                        var listData = fileContents.Split('\n').Select(q => q.Split(',').ToList()).ToList();
                        rule.FilterParameter = listData[0][0];
                        rule.ParameterName = listData[0][1];
                        rule.DrivenParameters = listData[0].Skip(2).Select(w => w.TrimEnd('\r').TrimEnd('\n')).ToList();
                        listData = listData.Skip(1).ToList();
                        var keys = listData.Select(q => q[0]).Distinct();
                        rule.DictKeyValues = new Dictionary<string, List<List<string>>>();
                        foreach (var key in keys)
                        {
                            var allForThisKey = listData.Where(q => q[0] == key);
                            var valuesForThisKey = allForThisKey.Select(q => q.Skip(1).Select(w => w.TrimEnd('\r').TrimEnd('\n')).ToList()).ToList();
                            rule.DictKeyValues.Add(key, valuesForThisKey);
                        }
                    }
                }

                if (rule.KeyValues != null)
                {
                    if (rule.DictKeyValues == null)
                    {
                        rule.DictKeyValues = new Dictionary<string, List<List<string>>>();
                    }
                    rule.DictKeyValues.Add("", rule.KeyValues);
                }

                if (rule.ListSource != null)
                {
                    if (rule.ListOptions != null)
                    {
                        Utils.Log($"Rule should not have both ListOptions {rule.ListOptions} and ListSource {rule.ListSource}", LogLevel.Error);
                    }

                    var fileContents = GetFileContents(rule.ListSource, ruleFileInfo.FilePath);

                    if (fileContents != null)
                    {
                        var listData = fileContents.Split('\n').Select(q => q.Split(',').ToList()).ToList();

                        if (listData != null)
                        {
                            rule.ListOptions = listData.Select(q => new ListOption { Name = q[0].TrimEnd('\r').TrimEnd('\n') }).ToList();
                        }
                    }
                }

                if (rule.FilterParameter != null)
                {
                    var paramId = GlobalParametersManager.FindByName(Utils.doc, rule.FilterParameter);
                    if (paramId != null)
                    {
                        if (Utils.doc.GetElement(paramId) is GlobalParameter param)
                        {
                            UpdaterRegistry.AddTrigger(
                                DataValidationUpdaterId,
                                Utils.doc,
                                new List<ElementId> { paramId },
                                Element.GetChangeTypeAny());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogException($"Cannot add trigger for rule: {rule}", ex);
            }

            if (Utils.doc == null)
            {
                var failureId = new FailureDefinitionId(Guid.NewGuid());
                var message = rule.UserMessage;
                if (string.IsNullOrEmpty(message))
                {
                    message = RULE_DEFAULT_MESSAGE;
                }
                try
                {
                    FailureDefinition.CreateFailureDefinition(
                        failureId,
                        FailureSeverity.Error,
                        message);
                }
                catch
                {
                }
                rule.FailureId = failureId;
            }
            else // https://forums.autodesk.com/t5/revit-ideas/api-allow-failuredefinition-createfailuredefinition-during/idi-p/12544647
            {
                rule.FailureId = genericFailureId;
            }
        }

        private static string GetFileContents(string fileName, string ruleInfoFilePath)
        {
            var path = Path.Combine(Utils.dllPath, fileName);
            string fileContents = null;

            if (Utils.Debugging && File.Exists(path))
            {
                using (var v = new StreamReader(new FileStream(path, System.IO.FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    fileContents = v.ReadToEnd();
                }
            }
            else
            {
                var data = Utils.GetGitData(ContentType.File, $"{ruleInfoFilePath}/{fileName}");
                if (data == null)
                {
                    Utils.Log($"File not found at {ruleInfoFilePath}", LogLevel.Error);
                }
                else
                {
                    Utils.Log($"Found {ruleInfoFilePath}", LogLevel.Trace);
                    fileContents = data.Content;
                }
            }
            return fileContents;
        }

        private static void HandleDeserializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
        {
            var currentError = e.ErrorContext.Error.Message;
            Utils.Log($"Error deserializing JSON: {currentError}", LogLevel.Error);
            e.ErrorContext.Handled = true;
        }
    }
}