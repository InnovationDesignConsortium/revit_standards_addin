using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RevitDataValidator
{
    internal class Ribbon : IExternalApplication
    {
        private readonly string ADDINS_FOLDER = @"C:\ProgramData\Autodesk\Revit\Addins";
        private readonly string RULE_FILE_EXT = ".md";
        private readonly string RULES = "Rules";
        private readonly string PARAMETER_PACK_FILE_NAME = "ParameterPacks.json";
        private const string NONE = "<none>";
        private readonly string RULE_DEFAULT_MESSAGE = "This is not allowed. (A default error message is given because the rule registered after Revit startup)";
        private FailureDefinitionId genericFailureId;
        public static UpdaterId DataValidationUpdaterId;

        public Result OnStartup(UIControlledApplication application)
        {
            Utils.dictCategoryPackSet = new Dictionary<string, string>();
            Utils.dictCustomCode = new Dictionary<string, Type>();
            Utils.app = application.ControlledApplication;
            Utils.errors = new List<string>();
            Utils.allParameterRules = new List<ParameterRule>();
            Utils.allWorksetRules = new List<WorksetRule>();
            application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;
            application.ViewActivated += Application_ViewActivated;
            application.DialogBoxShowing += Application_DialogBoxShowing;
            application.Idling += Application_Idling;
            Utils.eventHandlerWithParameterObject = new EventHandlerWithParameterObject();
            Utils.eventHandlerCreateInstancesInRoom = new EventHandlerCreateInstancesInRoom();

            Utils.paneId = new DockablePaneId(Guid.NewGuid());
            GetParameterPacks();
            if (Utils.parameterUIData != null)
            {
                Utils.propertiesPanel = new PropertiesPanel();
            }

            if (Utils.propertiesPanel != null)
            {
                application.RegisterDockablePane(Utils.paneId, "Properties Panel", Utils.propertiesPanel as IDockablePaneProvider);
                application.SelectionChanged += Application_SelectionChanged;
            }

            foreach (BuiltInCategory bic in Enum.GetValues(typeof(BuiltInCategory)))
            {
                try
                {
                    Utils.catMap.Add(LabelUtils.GetLabelFor(bic), bic);
                }
                catch
                { }
            }

            DataValidationUpdater dataValidationUpdater = new DataValidationUpdater(application.ActiveAddInId);
            DataValidationUpdaterId = dataValidationUpdater.GetUpdaterId();
            UpdaterRegistry.RegisterUpdater(dataValidationUpdater, true);

            genericFailureId = new FailureDefinitionId(Guid.NewGuid());
            FailureDefinition.CreateFailureDefinition(
                genericFailureId,
                FailureSeverity.Error,
                RULE_DEFAULT_MESSAGE);

            const string panelName = "Data Validator";
            var panel = application.GetRibbonPanels().Find(q => q.Name == panelName) ?? application.CreateRibbonPanel(panelName);
            var dll = typeof(Ribbon).Assembly.Location;
            Utils.dllPath = Path.GetDirectoryName(dll);

            panel.AddItem(new PushButtonData("ShowPaneCommand", "Show Pane", dll, "RevitDataValidator.ShowPaneCommand"));
            var cboRuleFile = panel.AddItem(new ComboBoxData("cboRuleFile")) as Autodesk.Revit.UI.ComboBox;
            var ruleFiles = Directory.GetFiles(Path.Combine(ADDINS_FOLDER, Utils.PRODUCT_NAME, RULES), "*" + RULE_FILE_EXT)
                .OrderBy(q => q);
            if (ruleFiles.Any())
            {
                foreach (string ruleFile in ruleFiles)
                {
                    var member = cboRuleFile.AddItem(new ComboBoxMemberData(ruleFile, Path.GetFileNameWithoutExtension(ruleFile)));
                    if (ruleFile == Properties.Settings.Default.ActiveRuleFile)
                    {
                        cboRuleFile.Current = member;
                    }
                }
                cboRuleFile.AddItem(new ComboBoxMemberData(NONE, NONE));

                RegisterRules();

                cboRuleFile.CurrentChanged += cboRuleFile_CurrentChanged;
            }

            if (Utils.errors.Any())
            {
                var errorfile = Path.Combine(Path.GetDirectoryName(Path.GetTempPath()), @"..\RevitValidator-ErrorLog-" + DateTime.Now.ToString().Replace(":", "-").Replace("/", "_") + ".txt");
                using (StreamWriter sw = new StreamWriter(errorfile, true))
                {
                    sw.Write(string.Join(Environment.NewLine, Utils.errors));
                }
                Process.Start(errorfile);
            }

            return Result.Succeeded;
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
            Utils.doc = e.Document;
        }

        private void cboRuleFile_CurrentChanged(object sender, ComboBoxCurrentChangedEventArgs e)
        {
            Properties.Settings.Default.ActiveRuleFile = e.NewValue.Name;
            Properties.Settings.Default.Save();
            Utils.allParameterRules = new List<ParameterRule>();
            Utils.allWorksetRules = new List<WorksetRule>();
            RegisterRules();
            Utils.propertiesPanel.Refresh();
        }

        private void SetupPane()
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
            if (Utils.selectedIds.Any())
            {
                element = doc.GetElement(Utils.selectedIds[0]);
            }
            else
            {
                element = doc.ActiveView;
            }

            if (element.Category == null)
                return;

            var catName = element.Category.Name;
            var validPacks = Utils.parameterUIData.PackSets.Where(q => q.Category == catName);
            if (!validPacks.Any())
            {
                Utils.propertiesPanel.Refresh(null);
                return;
            }

            PackSet packSet = null;
            if (Utils.dictCategoryPackSet.ContainsKey(catName))
                packSet = validPacks.FirstOrDefault(q => q.Name == Utils.dictCategoryPackSet[catName]);

            if (packSet == null)
                packSet = validPacks.First();

            if (packSet != null)
            {
                var packSetName = packSet.Name;
                try
                {
                    Utils.propertiesPanel.cbo.SelectedItem = packSetName;
                    Utils.propertiesPanel.Refresh(packSetName);
                }
                catch (Exception ex)
                {
                }
            }
        }

        private void Application_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Utils.selectedIds = e.GetSelectedElements().ToList();
            SetupPane();
        }

        private void ControlledApplication_DocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
        {
            Utils.selectedIds = new List<ElementId>();
            Utils.doc = e.Document;
            SetupPane();
        }

        public void RegisterRules()
        {
            Utils.Log($"Registering rules");
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
                    (Utils.doc != null && q.RevitFileNames != null && q.RevitFileNames.Contains(Path.GetFileNameWithoutExtension(Utils.doc.PathName)))))
                {
                    RegisterParameterRule(parameterRule);
                    Utils.allParameterRules.Add(parameterRule);
                }
            }
            if (worksetRules != null)
            {
                foreach (var worksetRule in worksetRules.Where(q =>
                    q.RevitFileNames == null ||
                    (Utils.doc != null && q.RevitFileNames?.Contains(Path.GetFileNameWithoutExtension(Utils.doc.PathName)) == true)))
                {
                    RegisterWorksetRule(worksetRule);
                    Utils.allWorksetRules.Add(worksetRule);
                }
            }
        }

        private void RegisterWorksetRule(WorksetRule worksetRule)
        {
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
            Utils.Log(" Registering rule " + rule);
            try
            {
                if (rule.CustomCode != null)
                {
                    var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var filename = Path.Combine(assemblyFolder, rule.CustomCode + ".txt");
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
            Utils.Log("Completed registering rule " + rule.ToString());
        }

        private void GetParameterPacks()
        {
            var file = Path.Combine(ADDINS_FOLDER, Utils.PRODUCT_NAME, PARAMETER_PACK_FILE_NAME);
            if (!File.Exists(file))
                return;
            var json = File.ReadAllText(file);
            Utils.parameterUIData = JsonConvert.DeserializeObject<ParameterUIData>(json);
        }

        private void GetRules(out List<ParameterRule> parameterRules, out List<WorksetRule> worksetRules)
        {
            parameterRules = new List<ParameterRule>();
            worksetRules = new List<WorksetRule>();
            var ruleFile = Properties.Settings.Default.ActiveRuleFile;
            if (ruleFile == NONE)
            {
                return;
            }

            if (File.Exists(ruleFile))
            {
                var markdown = File.ReadAllText(Properties.Settings.Default.ActiveRuleFile);
                MarkdownDocument document = Markdown.Parse(markdown);
                var descendents = document.Descendants();
                var codeblocks = document.Descendants<FencedCodeBlock>().ToList();
                foreach (var block in codeblocks)
                {
                    var lines = block.Lines.Cast<StringLine>().Select(q => q.ToString()).ToList();
                    var json = string.Concat(lines.Where(q => !q.StartsWith("//")).ToList());
                    RuleData rules = null;
                    try
                    {
                        rules = JsonConvert.DeserializeObject<RuleData>(json);
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
            else
            {
                Utils.Log("File not found: " + Properties.Settings.Default.ActiveRuleFile, Utils.LogLevel.Error);
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}