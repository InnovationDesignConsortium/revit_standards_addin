using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CsvHelper;
using CsvHelper.Configuration;
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
using System.Globalization;
using System.IO;
using System.Linq;
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
                    ft.FileName = string.Concat(ft.FileName.ToString().Substring(0, ft.FileName.ToString().Length - 4), " ", DateTime.Now.ToString().Replace(":", "-").Replace("/", "_"), ".log");
                }
            }
            LogManager.Configuration = logConfig;

            Utils.Log($"Running version: {Utils.GetInstalledVersion()}", LogLevel.Trace);

            Utils.token_for_GIT_CODE_REPO_OWNER = Utils.GetGithubTokenFromApp(Utils.GIT_CODE_REPO_OWNER);

            Utils.dictCategoryPackSet = new Dictionary<string, string>();
            Utils.dictCustomCode = new Dictionary<string, Type>();
            Utils.app = Application.ControlledApplication;
            Utils.allParameterRules = new List<ParameterRule>();
            Utils.allWorksetRules = new List<WorksetRule>();
            Application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;
            Application.ControlledApplication.DocumentSaving += ControlledApplication_DocumentSaving;
            Application.ControlledApplication.DocumentSavedAs += ControlledApplication_DocumentSavedAs;
            Application.ControlledApplication.DocumentSynchronizingWithCentral += ControlledApplication_DocumentSynchronizingWithCentral;
            Application.ViewActivated += Application_ViewActivated;
            Application.DialogBoxShowing += Application_DialogBoxShowing;
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

            var showLogsCommand = new PushButtonData("ShowLogCommand", "Show\nLog", dll, "RevitDataValidator.ShowLogCommand")
            {
                Image = NewBitmapImage(GetType().Namespace, "log16.png"),
                LargeImage = NewBitmapImage(GetType().Namespace, "log.png")
            };
            panel.AddItem(showLogsCommand);

            var aboutCommand = new PushButtonData("AboutCommand", "About", dll, "RevitDataValidator.AboutCommand")
            {
                Image = NewBitmapImage(GetType().Namespace, "about16.png"),
                LargeImage = NewBitmapImage(GetType().Namespace, "about.png")
            };
            panel.AddItem(aboutCommand);

            Update.CheckForUpdates();
        }

        private void ControlledApplication_DocumentSavedAs(object sender, Autodesk.Revit.DB.Events.DocumentSavedAsEventArgs e)
        {
            Utils.doc = e.Document;
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
                    parameterPackFileContents = GetFileContents(PARAMETER_PACK_FILE_NAME, parameterPackFilePath).Contents;
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
                if (Utils.ruleDatas.TryGetValue(newFilename, out RuleFileInfo cachedRuleFileInfo))
                {
                    ruleFileInfo = cachedRuleFileInfo;
                }
                else
                {
                    gitRuleFilePath = GetGitFileNamesFromConfig();
                    ruleFileInfo = GetFileContents(RULE_FILE_NAME, gitRuleFilePath);

                    if (ruleFileInfo == null)
                    {
                        Utils.ruleDatas.Add(newFilename, new RuleFileInfo());
                        Utils.propertiesPanel.Refresh();
                        return;
                    }
                    else
                    {
                        Utils.ruleDatas.Add(newFilename, ruleFileInfo);
                    }
                }

                var parameterRules = new List<ParameterRule>();
                var worksetRules = new List<WorksetRule>();

                MarkdownDocument document = Markdown.Parse(ruleFileInfo.Contents);
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
                        if (rules.ParameterRules != null)
                        {
                            parameterRules.AddRange(rules.ParameterRules);
                        }
                        if (rules.WorksetRules != null)
                        {
                            worksetRules.AddRange(rules.WorksetRules);
                        }
                    }
                }

                if (parameterRules != null)
                {
                    foreach (var parameterRule in parameterRules)
                    {
                        ParameterRule conflictingRule = null;
                        RegisterParameterRule(parameterRule, ruleFileInfo);
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
                            parameterRule.Guid = Guid.NewGuid();
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
                            worksetRule.Guid = Guid.NewGuid();
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
            if ((r1.Categories != null && (r1.Categories.Contains(Utils.ALL) || r2.Categories.Contains(Utils.ALL) || r1.Categories.Intersect(r2.Categories).Any())) ||
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
            Utils.GetEnvironmentVariableData();
            var projectName = Utils.GetFileName();

            var path = "Standards/RevitStandardsPanel/Config.json";

            var json = "";
            if (Utils.LOCAL_FILE_PATH != null)
            {
                var localpath = Path.Combine(Utils.LOCAL_FILE_PATH, path);
                if (File.Exists(localpath))
                {
                    json = File.ReadAllText(localpath);
                }
            }
            else
            {
                var data = Utils.GetGitData(ContentType.File, path);
                if (data == null)
                {
                    Utils.Log($"No git data at {path}", LogLevel.Warn);
                    return null;
                }
                json = data.Content;
            }
            if (json == "")
            {
                Utils.Log($"File does not exist: {path}", LogLevel.Error);
                return null;
            }
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
                var validPacks = Utils.parameterUIData.PackSets.Where(q => q.Category == catName || q.Category == Utils.ALL).ToList();
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
                    string code = GetFileContents($"{rule.CustomCode}.cs", gitRuleFilePath).Contents;
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

                    if (fileContents?.Contents != null)
                    {
                        using (var csv = new CsvReader(new StringReader(fileContents.Contents), new CsvConfiguration(CultureInfo.InvariantCulture)
                        {
                            HasHeaderRecord = false,
                            MissingFieldFound = null
                        }))
                        {
                            var i = 0;
                            rule.DictKeyValues = new Dictionary<string, List<List<string>>>();
                            var listData = new List<List<string>>();
                            while (csv.Read())
                            {
                                if (i == 0)
                                {
                                    rule.FilterParameter = csv[0];
                                    rule.ParameterName = csv[1];
                                    rule.DrivenParameters = csv.Parser.Record.ToList().Skip(2).ToList();
                                }
                                else
                                {
                                    listData.Add(csv.Parser.Record.ToList());
                                }
                                i++;
                            }
                            var keys = listData.Select(q => q[0]).Distinct();
                            rule.DictKeyValues = new Dictionary<string, List<List<string>>>();
                            foreach (var key in keys)
                            {
                                var allForThisKey = listData.Where(q => q[0] == key);
                                var valuesForThisKey = allForThisKey.Select(q => q.Skip(1).ToList()).ToList();
                                rule.DictKeyValues.Add(key, valuesForThisKey);
                            }
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
                        using (var csv = new CsvReader(new StringReader(fileContents.Contents), new CsvConfiguration(CultureInfo.InvariantCulture)
                        {
                            HasHeaderRecord = false,
                            MissingFieldFound = null
                        }))
                        {
                            rule.ListOptions = csv.GetRecords<ListOption>().ToList();
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

        private static RuleFileInfo GetFileContents(string fileName, string ruleInfoFilePath)
        {
            var ret = new RuleFileInfo();
            if (Utils.LOCAL_FILE_PATH != null)
            {
                if (ruleInfoFilePath.StartsWith("/"))
                {
                    ruleInfoFilePath = ruleInfoFilePath.Substring(1);
                }

                var path = Path.Combine(Utils.LOCAL_FILE_PATH, ruleInfoFilePath);
                var fullpath = Path.Combine(path, fileName);
                ret.FilePath = path;
                if (File.Exists(fullpath))
                {
                    ret.Filename = fullpath;
                    using (var v = new StreamReader(new FileStream(fullpath, System.IO.FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                    {
                        ret.Contents = v.ReadToEnd();
                    }
                }
                else
                {
                    Utils.Log($"File not found: {fullpath}", LogLevel.Error);
                }
            }
            else
            {
                var data = Utils.GetGitData(ContentType.File, $"{ruleInfoFilePath}/{fileName}");
                if (data == null)
                {
                    Utils.Log($"File not found: {ruleInfoFilePath}/{fileName}", LogLevel.Error);
                }
                else
                {
                    Utils.Log($"Found {ruleInfoFilePath}", LogLevel.Trace);
                    ret.FilePath = ruleInfoFilePath;
                    ret.Url = data.HtmlUrl;
                    ret.Contents = data.Content;
                }
            }
            return ret;
        }

        private static void HandleDeserializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
        {
            var currentError = e.ErrorContext.Error.Message;
            Utils.Log($"Error deserializing JSON: {currentError}", LogLevel.Error);
            e.ErrorContext.Handled = true;
        }
    }
}