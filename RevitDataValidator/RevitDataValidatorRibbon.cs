using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using RevitDataValidator.Classes;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RevitDataValidator
{
    internal class Ribbon : Nice3point.Revit.Toolkit.External.ExternalApplication
    {
        private const string RULE_FILE_NAME = "rules.md";
        private readonly string PARAMETER_PACK_FILE_NAME = "parameterpacks.json";
        private readonly string RULE_DEFAULT_MESSAGE = "This is not allowed. (A default error message is given because the rule registered after Revit startup)";
        private FailureDefinitionId genericFailureId;
        public static UpdaterId DataValidationUpdaterId;
        private static string GIT_OWNER = "";
        private static string GIT_REPO = "";
        private const string OWNER_ENV = "RevitStandardsAddinGitOwner";
        private const string REPO_ENV = "RevitStandardsAddinGitRepo";


        public override void OnStartup()
        {
            Utils.dictCategoryPackSet = new Dictionary<string, string>();
            Utils.dictCustomCode = new Dictionary<string, Type>();
            Utils.app = Application.ControlledApplication;
            Utils.errors = new List<string>();
            Utils.allParameterRules = new List<ParameterRule>();
            Utils.allWorksetRules = new List<WorksetRule>();
            Application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;
            Application.ViewActivated += Application_ViewActivated;
            Application.DialogBoxShowing += Application_DialogBoxShowing;
            Application.ControlledApplication.DocumentClosed += ControlledApplication_DocumentClosed;
            Application.Idling += Application_Idling;
            Utils.eventHandlerWithParameterObject = new EventHandlerWithParameterObject();
            Utils.eventHandlerCreateInstancesInRoom = new EventHandlerCreateInstancesInRoom();

            GIT_OWNER = Environment.GetEnvironmentVariable(OWNER_ENV, EnvironmentVariableTarget.Machine);
            if (GIT_OWNER == null)
            {
                Environment.SetEnvironmentVariable(OWNER_ENV, "", EnvironmentVariableTarget.Machine);
                Utils.Log($"Environment variable {OWNER_ENV} is empty", Utils.LogLevel.Error);
            }
            GIT_REPO = Environment.GetEnvironmentVariable(REPO_ENV, EnvironmentVariableTarget.Machine);
            if (GIT_REPO == null)
            {
                Environment.SetEnvironmentVariable(REPO_ENV, "", EnvironmentVariableTarget.Machine);
                Utils.Log($"Environment variable {REPO_ENV} is empty", Utils.LogLevel.Error);
            }

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

            // change tab name below in ClearComboBoxItems() as needed
            var panel = Application.GetRibbonPanels().Find(q => q.Name == Utils.panelName) ?? Application.CreateRibbonPanel(Utils.panelName);
            var dll = typeof(Ribbon).Assembly.Location;
            Utils.dllPath = Path.GetDirectoryName(dll);

            panel.AddItem(new PushButtonData("ShowPaneCommand", "Show Pane", dll, "RevitDataValidator.ShowPaneCommand"));
            panel.AddItem(new PushButtonData("AboutCommand", "About", dll, "RevitDataValidator.AboutCommand"));
            ShowErrors();
            Update.CheckForUpdates();
        }

        public override void OnShutdown()
        {
            if (Utils.MsiToRunOnExit != null)
            {
                Utils.Log($"Installing new version {Utils.MsiToRunOnExit}", Utils.LogLevel.Trace);
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

        private void ControlledApplication_DocumentClosed(object sender, Autodesk.Revit.DB.Events.DocumentClosedEventArgs e)
        {
            Utils.doc = null;
        }

        private void ControlledApplication_DocumentChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
        {
            Utils.propertiesPanel?.Refresh();
        }

        private static void ShowErrors()
        {
            if (Utils.errors.Count != 0)
            {
                var errorfile = Path.Combine(Path.GetDirectoryName(Path.GetTempPath()), @"..\RevitValidator-ErrorLog-" + DateTime.Now.ToString().Replace(":", "-").Replace("/", "_") + ".txt");
                using (StreamWriter sw = new StreamWriter(errorfile, true))
                {
                    sw.Write(string.Join(Environment.NewLine, Utils.errors));
                }
                Utils.StartShell(errorfile, true);
                Utils.errors.Clear();
            }
        }

        private void Application_Idling(object sender, IdlingEventArgs e)
        {
            Utils.dialogIdShowing = "";
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
                    var file = Path.Combine(Utils.dllPath, PARAMETER_PACK_FILE_NAME);
                    string json = "";
                    if (File.Exists(file))
                    {
                        json = File.ReadAllText(file);
                        Utils.Log($"Read parameter packs from {file}", Utils.LogLevel.Info);
                    }
                    else
                    {
                        var data = GetGitData(ContentType.File, $"{parameterPackFilePath}/{PARAMETER_PACK_FILE_NAME}", Utils.githubToken);
                        if (data == null)
                        {
                            Utils.Log($"No parameter pack data at {parameterPackFilePath}", Utils.LogLevel.Warn);
                        }
                        else
                        {
                            Utils.Log($"Found parameter pack file {parameterPackFilePath}", Utils.LogLevel.Trace);
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
                    ShowErrors();
                }

                string ruleFileContents = null;
                if (Utils.ruleDatas.TryGetValue(newFilename, out RuleFileInfo cachedRuleFileInfo))
                {
                    ruleFileContents = cachedRuleFileInfo.Contents;
                }
                else
                {
                    var gitRuleFilePath = GetGitFileNamesFromConfig();
                    RepositoryContent ruleData = null;
                    var ruleFileInfo = new RuleFileInfo();
                    if (gitRuleFilePath != null)
                    {
                        ruleData = GetGitData(ContentType.File, $"{gitRuleFilePath}/{RULE_FILE_NAME}", Utils.githubToken);
                        ruleFileInfo.Url = ruleData.HtmlUrl;
                        ruleFileContents = ruleData.Content;
                    }

                    if (ruleData == null)
                    {
                        var ruleFile = Directory.GetFiles(Utils.dllPath).FirstOrDefault(q => Path.GetFileName(q) == RULE_FILE_NAME);
                        if (ruleFile != null)
                        {
                            using (var reader = new StreamReader(ruleFile))
                            {
                                ruleFileContents = reader.ReadToEnd();
                            }
                            ruleFileInfo.Filename = ruleFile;
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
                        ShowErrors();
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
                            RegisterParameterRule(parameterRule);
                            Utils.allParameterRules.Add(parameterRule);
                        }
                        else
                        {
                            Utils.Log($"Ignoring parameter rule '{parameterRule}' because it conflicts with the rule '{conflictingRule}'", Utils.LogLevel.Error);
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
                            Utils.Log($"Ignoring workset rule '{worksetRule}' because it conflicts with the rule '{conflictingRule}'", Utils.LogLevel.Error);
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
            var data = GetGitData(ContentType.File, path, Utils.githubToken);

            if (data == null)
            {
                Utils.Log($"No git data at {path}", Utils.LogLevel.Warn);
                return null;
            }

            var json = data.Content;
            var configs = JsonConvert.DeserializeObject<GitRuleConfigRoot>(json, new JsonSerializerSettings
            {
                Error = HandleDeserializationError,
                MissingMemberHandling = MissingMemberHandling.Error
            });
            ShowErrors();

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

        private static RepositoryContent GetGitData(ContentType contentType, string path, string privateToken = null)
        {
            GitHubClient client;
            if (privateToken == null)
            {
                client = new GitHubClient(new ProductHeaderValue("revit-datavalidator"))
                {
                    Credentials = new Credentials("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")
                };
            }
            else
            {
                client = new GitHubClient(new ProductHeaderValue("revit-datavalidator"))
                {
                    Credentials = new Credentials(privateToken)
                };
            }
            try
            {
                var content = client.Repository.Content.GetAllContents(GIT_OWNER, GIT_REPO, path);
                if (content == null || content.IsFaulted)
                {
                    Utils.Log($"No git data found at {path}", Utils.LogLevel.Warn);
                    return null;
                }

                if (content.Result == null)
                {
                    Utils.Log($"No git data found at {path} for {contentType}", Utils.LogLevel.Warn);
                    return null;
                }

                var result = content.Result.Where(q => q.Type == contentType);
                if (result == null)
                {
                    Utils.Log($"No git data found at {path} for {contentType}", Utils.LogLevel.Warn);
                    return null;
                }
                return result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Utils.LogException("GetGitData", ex);
                return null;
            }
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

            if (element.Category == null)
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
        }

        private static void RegisterWorksetRule(WorksetRule worksetRule)
        {
            Utils.Log("Registering workset rule " + worksetRule, Utils.LogLevel.Trace);
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
                    filter,
                    Element.GetChangeTypeAny());
                UpdaterRegistry.AddTrigger(
                    DataValidationUpdaterId,
                    filter,
                    Element.GetChangeTypeElementAddition());
            }
        }

        private void RegisterParameterRule(ParameterRule rule)
        {
            Utils.Log($"Registering parameter rule '{rule}'", Utils.LogLevel.Trace);
            try
            {
                if (rule.CustomCode != null)
                {
                    var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var filename = Path.Combine(assemblyFolder, rule.CustomCode + ".cs");
                    if (File.Exists(filename))
                    {
                        using (var sr = new StreamReader(filename))
                        {
                            var code = sr.ReadToEnd();
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
                                    Utils.Log($"{rule.CustomCode} compilation error: {error.GetMessage()}", Utils.LogLevel.Error);
                                }
                            }
                        }
                    }
                }
                if (rule.Categories != null)
                {
                    var builtInCats = Utils.GetBuiltInCats(rule);
                    UpdaterRegistry.AddTrigger(
                        DataValidationUpdaterId,
                        new ElementMulticategoryFilter(builtInCats),
                        Element.GetChangeTypeAny());
                }
                else if (rule.ElementClasses != null)
                {
                    var types = new List<Type>();
                    foreach (string className in rule.ElementClasses)
                    {
                        var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(q => q.CodeBase.IndexOf("revitapi.dll", StringComparison.OrdinalIgnoreCase) >= 0);
                        var type = asm.GetType(className);
                        if (type != null)
                            types.Add(type);
                    }
                    if (types.Count > 0)
                    {
                        UpdaterRegistry.AddTrigger(
                            DataValidationUpdaterId,
                            new ElementMulticlassFilter(types),
                            Element.GetChangeTypeAny());
                        UpdaterRegistry.AddTrigger(
                            DataValidationUpdaterId,
                            new ElementMulticlassFilter(types),
                            Element.GetChangeTypeElementAddition());
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

        private static void HandleDeserializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
        {
            var currentError = e.ErrorContext.Error.Message;
            Utils.Log($"Error deserializing JSON: {currentError}", Utils.LogLevel.Error);
            e.ErrorContext.Handled = true;
        }
    }
}