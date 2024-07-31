using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Windows;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ComboBox = Autodesk.Revit.UI.ComboBox;

namespace RevitDataValidator
{
    internal class Ribbon : Nice3point.Revit.Toolkit.External.ExternalApplication
    {
        private readonly string ADDINS_FOLDER = @"C:\ProgramData\Autodesk\Revit\Addins";
        private readonly string RULE_FILE_EXT = ".md";
        private readonly string RULES = "Rules";
        private readonly string PARAMETER_PACK_FILE_NAME = "ParameterPacks.json";
        private const string NONE = "<none>";
        private readonly string RULE_DEFAULT_MESSAGE = "This is not allowed. (A default error message is given because the rule registered after Revit startup)";
        private FailureDefinitionId genericFailureId;
        public static UpdaterId DataValidationUpdaterId;
        private static ComboBox cboRuleFile;

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
            cboRuleFile = panel.AddItem(new ComboBoxData(Utils.cboName)) as ComboBox;
            cboRuleFile.CurrentChanged += cboRuleFile_CurrentChanged;
            ShowErrors();
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
                Process.Start(errorfile);
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

        private void GetRulesAndParameterPacks()
        {
            GetParameterPacks();
            var ruleFiles = Directory.GetFiles(Path.Combine(ADDINS_FOLDER, Utils.PRODUCT_NAME, RULES), "*" + RULE_FILE_EXT)
                .OrderBy(q => q).ToList();
            if (ruleFiles.Count == 0)
            {
                ruleFiles = GetGitRuleFiles();
            }

            ClearComboBoxItems();

            if (ruleFiles?.Count > 0)
            {
                var wasDefaultRuleFileSet = false;
                var cbo = Utils.GetAdwindowsComboBox();
                foreach (string ruleFile in ruleFiles)
                {
                    AddComboBoxItem(ruleFile, Path.GetFileNameWithoutExtension(ruleFile));
                    if (ruleFile == Properties.Settings.Default.ActiveRuleFile)
                    {
                        cbo.Current = cbo.Items.Cast<Autodesk.Windows.RibbonItem>().FirstOrDefault(q => q.Text == ruleFile);
                        wasDefaultRuleFileSet = true;
                    }
                }

                if (!wasDefaultRuleFileSet)
                {
                    cbo.Current = cbo.Items[0];
                    Properties.Settings.Default.ActiveRuleFile = ruleFiles[0];
                    Properties.Settings.Default.Save();
                }

                AddComboBoxItem(NONE, NONE);

                RegisterRules();
            }
            Utils.propertiesPanel.Refresh();
        }

        private static void ClearComboBoxItems()
        {
            var combobox = Utils.GetAdwindowsComboBox();
            if (combobox == null)
                return;
            combobox.Items.Clear();
        }

        private static void AddComboBoxItem(string id, string text)
        {
            var combobox = Utils.GetAdwindowsComboBox();
            if (combobox == null)
                return;

            combobox.Items.Add(new Autodesk.Windows.RibbonItem
            {
                Id = id,
                Text = text
            });
        }

        private void Application_ViewActivated(object sender, ViewActivatedEventArgs e)
        {
            var currentFilename = Utils.GetFileName(e.Document);
            if (Utils.doc == null || currentFilename != Utils.doc.PathName)
            {
                Utils.allParameterRules.Clear();
                Utils.allWorksetRules.Clear();
                Utils.doc = e.Document;
                Utils.userName = e.Document.Application.Username;
                GetRulesAndParameterPacks();
                var cbo = Utils.GetAdwindowsComboBox();
                var ruleOptions = cbo.Items.Cast<Autodesk.Windows.RibbonItem>();
                if (ruleOptions.Any())
                {
                    if (Properties.Settings.Default.ActiveRuleFile != null)
                    {
                        var defaultRuleFileOption = ruleOptions.FirstOrDefault(q => q.Text == Properties.Settings.Default.ActiveRuleFile);
                        if (defaultRuleFileOption != null)
                        {
                            cbo.Current = defaultRuleFileOption;
                        }
                    }
                    else if (Utils.activeRuleFiles.TryGetValue(Utils.GetFileName(), out string value))
                    {
                        Properties.Settings.Default.ActiveRuleFile = value;
                        Properties.Settings.Default.Save();
                        if (value != null)
                        {
                            if (ruleOptions.Any())
                            {
                                cbo.Current = ruleOptions.FirstOrDefault(q =>
                                    q.Text == value);
                            }
                        }
                    }
                    else
                    {
                        Utils.activeRuleFiles.Add(Utils.GetFileName(), null);
                    }
                }
                SetupPane();
            }
        }

        public static string GetGitParameterPacks()
        {
            var projectName = Path.GetFileNameWithoutExtension(Utils.GetFileName());
            var path = $"/ProjectRoot/{projectName}/Revit/RevitStandardsPanel/ParameterPacks/ParameterPacks.json";
            var data = GetGitData("ParameterPacks.json", ContentType.File, path);
            if (data == null || !data.Any())
            {
                Utils.Log($"No git data at {path}", Utils.LogLevel.Warn);
                return null;
            }
            Utils.Log($"Found git file {path}", Utils.LogLevel.Trace);
            var packs = data?.FirstOrDefault().Content;
            return packs;
        }

        public static string GetGitRuleFileContents(string filename)
        {
            Utils.GitRuleFileUrl = null;
            var projectName = Path.GetFileNameWithoutExtension(Utils.GetFileName());

            var path = $"/ProjectRoot/{projectName}/Revit/RevitStandardsPanel/Rules/{filename}.md";
            var data = GetGitData($"{filename}.md", ContentType.File, path);
            if (data == null || !data.Any())
            {
                Utils.Log($"No git data at {path}", Utils.LogLevel.Warn);
                return null;
            }
            Utils.GitRuleFileUrl = data.First().HtmlUrl;
            return data?.First().Content;
        }

        private static List<string> GetGitRuleFiles()
        {
            if (Utils.doc == null || Utils.GetFileName().Length == 0)
            {
                return null;
            }

            var projectName = Path.GetFileNameWithoutExtension(Utils.GetFileName());

            var path = $"/ProjectRoot/{projectName}/Revit/RevitStandardsPanel/Rules";
            var data = GetGitData(null, ContentType.File, path);
            if (data == null || !data.Any())
            {
                Utils.Log($"No git data at {path}", Utils.LogLevel.Warn);
                return null;
            }

            var ruleFiles = data.Select(q => q.Name).ToList();
            Utils.Log($"Found git rule files '{string.Join(",", ruleFiles)}'", Utils.LogLevel.Trace);
            return ruleFiles;
        }

        private static IEnumerable<RepositoryContent> GetGitData(string projectName, ContentType contentType, string path)
        {
            var client = new GitHubClient(new Octokit.ProductHeaderValue("revit-datavalidator"))
            {
                Credentials = new Credentials("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")
            };
            const string OWNER = "InnovationDesignConsortium";
            const string REPO = "revit_standards_settings_demo";
            try
            {
                var content = client.Repository.Content.GetAllContents(OWNER, REPO, path);
                if (content == null || content.IsFaulted)
                {
                    Utils.Log($"No git data found at {path}", Utils.LogLevel.Warn);
                    return new List<RepositoryContent>();
                }

                var result = content.Result.Where(q => q.Type == contentType);
                if (result == null)
                {
                    Utils.Log($"No git data found at {path} for {contentType}", Utils.LogLevel.Warn);
                    return new List<RepositoryContent>();
                }

                if (projectName != null)
                {
                    result = result.Where(q => q.Name == projectName);
                }
                return result;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private void cboRuleFile_CurrentChanged(object sender, ComboBoxCurrentChangedEventArgs e)
        {
            Utils.allParameterRules = new List<ParameterRule>();
            Utils.allWorksetRules = new List<WorksetRule>();
            Utils.GitRuleFileUrl = null;

            var cbo = Utils.GetAdwindowsComboBox();

            if (cbo.Items.Count == 0)
            {
                return;
            }

            var current = cbo.Current;
            var ri = current as Autodesk.Windows.RibbonItem;
            Utils.activeRuleFiles[Utils.GetFileName()] = ri.Text;
            Properties.Settings.Default.ActiveRuleFile = ri.Text;
            Properties.Settings.Default.Save();
            RegisterRules();
            Utils.propertiesPanel.Refresh();
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

        public void RegisterRules()
        {
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
            GetRules(out List<ParameterRule> parameterRules, out List<WorksetRule> worksetRules);

            if (parameterRules != null)
            {
                foreach (var parameterRule in parameterRules.Where(q =>
                    q.RevitFileNames == null ||
                    (Utils.doc != null && q.RevitFileNames != null && q.RevitFileNames.Contains(Path.GetFileNameWithoutExtension(Utils.GetFileName())))))
                {
                    RegisterParameterRule(parameterRule);
                    Utils.allParameterRules.Add(parameterRule);
                }
            }
            if (worksetRules != null)
            {
                foreach (var worksetRule in worksetRules.Where(q =>
                    q.RevitFileNames == null ||
                    (Utils.doc != null && q.RevitFileNames?.Contains(Path.GetFileNameWithoutExtension(Utils.GetFileName())) == true)))
                {
                    RegisterWorksetRule(worksetRule);
                    Utils.allWorksetRules.Add(worksetRule);
                }
            }
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

        private void GetParameterPacks()
        {
            var file = Path.Combine(ADDINS_FOLDER, Utils.PRODUCT_NAME, PARAMETER_PACK_FILE_NAME);
            string json = "";
            if (File.Exists(file))
            {
                json = File.ReadAllText(file);
                Utils.Log($"Read parameter packs from {file}", Utils.LogLevel.Info);
            }
            else if (!string.IsNullOrEmpty(Utils.GetFileName()))
            {
                json = GetGitParameterPacks();
            }
            if (string.IsNullOrEmpty(json))
            {
                Utils.parameterUIData = new ParameterUIData();
                return;
            }
            Utils.parameterUIData = JsonConvert.DeserializeObject<ParameterUIData>(json, new JsonSerializerSettings
            {
                Error = HandleDeserializationError,
                MissingMemberHandling = MissingMemberHandling.Error
            });
        }

        private void GetRules(out List<ParameterRule> parameterRules, out List<WorksetRule> worksetRules)
        {
            parameterRules = new List<ParameterRule>();
            worksetRules = new List<WorksetRule>();
            var ruleFile = Properties.Settings.Default.ActiveRuleFile;
            if (ruleFile == NONE || ruleFile?.Length == 0)
            {
                return;
            }
            var fileContents = "";
            if (File.Exists(ruleFile))
            {
                fileContents = File.ReadAllText(ruleFile);
                Utils.Log($"Read rules from {ruleFile}", Utils.LogLevel.Info);
            }
            else
            {
                var cbo = Utils.GetAdwindowsComboBox();
                var cboItems = cbo.Items.Cast<Autodesk.Windows.RibbonItem>().ToList();
                var fileFromDisk = cboItems.Find(q => q.Text == ruleFile).Id;
                if (fileFromDisk != null)
                {
                    fileContents = File.ReadAllText(fileFromDisk);
                    Utils.Log($"Read rules from {fileFromDisk}", Utils.LogLevel.Info);
                }
                else
                {
                    fileContents = GetGitRuleFileContents(Path.GetFileNameWithoutExtension(Properties.Settings.Default.ActiveRuleFile));
                }
            }

            if (string.IsNullOrEmpty(fileContents))
            {
                Utils.Log($"File not found '{Properties.Settings.Default.ActiveRuleFile}'", Utils.LogLevel.Error);
                return;
            }

            MarkdownDocument document = Markdown.Parse(fileContents);
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
        }

        private void HandleDeserializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
        {
            var currentError = e.ErrorContext.Error.Message;
            Utils.Log($"Error deserializing JSON in '{Path.GetFileName(Properties.Settings.Default.ActiveRuleFile)}': {currentError}", Utils.LogLevel.Error);
            e.ErrorContext.Handled = true;
        }
    }
}