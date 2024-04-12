using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RevitDataValidator
{
    internal class Ribbon : IExternalApplication
    {
        private readonly string ADDINS_FOLDER = @"C:\ProgramData\Autodesk\Revit\Addins";
        private readonly string RULE_FILE_NAME = "ProjectRules.md";
        private readonly string PARAMETER_PACK_FILE_NAME = "ParameterPacks.json";
        private readonly string RULE_DEFAULT_MESSAGE = "This is not allowed.";
        private FailureDefinitionId genericFailureId;
        public static UpdaterId DataValidationUpdaterId;

        public Result OnStartup(UIControlledApplication application)
        {
            Utils.dictCategoryPackSet = new Dictionary<string, string>();
            Utils.dictCustomCode = new Dictionary<string, Type>();
            Utils.app = application.ControlledApplication;
            Utils.errors = new List<string>();
            Utils.allRules = new List<Rule>();
            application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;
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
                application.ViewActivated += HideDockablePanelOnStartup;
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

            genericFailureId = new FailureDefinitionId(new Guid());
            RegisterRules(null);

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

        private void Application_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var doc = e.GetDocument();
            Utils.doc = doc;
            var app = doc.Application;
            var uiapp = new UIApplication(app);
            var pane = uiapp.GetDockablePane(Utils.paneId);
            var selectedElements = e.GetSelectedElements().Select(q => doc.GetElement(q)).ToList();
            if (selectedElements.Count() == 0)
            {
                Utils.propertiesPanel.SaveTextBoxValues();
                pane.Hide();
                return;
            }

            Utils.selectedIds = e.GetSelectedElements();
            var element = doc.GetElement(Utils.selectedIds.First());
            if (element.Category != null)
            {
                var catName = element.Category.Name;
                var validPacks = Utils.parameterUIData.PackSets.Where(q => q.Category == catName);
                if (validPacks.Count() == 0)
                {
                    pane.Hide();
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
                    Utils.propertiesPanel.cbo.SelectedItem = packSetName;
                    Utils.propertiesPanel.Refresh(packSetName);
                    pane.Show();
                }
                else
                {
                    pane.Hide();
                }
            }
            else
            {
                pane.Hide();
            }
        }

        private void HideDockablePanelOnStartup(object sender, ViewActivatedEventArgs e)
        {
            var uiapp = sender as UIApplication;
            try
            {
                var window = uiapp.GetDockablePane(Utils.paneId);
                window.Hide();
                uiapp.ViewActivated -= HideDockablePanelOnStartup;
            }
            catch
            { }
        }

        private void ControlledApplication_DocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
        {
            Utils.doc = e.Document;
            RegisterRules(e.Document.PathName);
        }

        public void RegisterRules(string filename)
        {
            if (filename != null)
            {
                try
                {
                    UpdaterRegistry.RemoveDocumentTriggers(DataValidationUpdaterId, Utils.doc);
                }
                catch (Exception ex)
                {
                }
            }

            var rules = GetRules();
            if (filename == null)
            {
                foreach (var rule in rules.Where(q =>
                    q.RevitFileNames == null))
                {
                    RegisterRule(rule);
                    Utils.allRules.Add(rule);
                }
            }
            else
            {
                foreach (var rule in rules.Where(q =>
                    q.RevitFileNames != null &&
                    q.RevitFileNames.Contains(Path.GetFileNameWithoutExtension(filename))))
                {
                    RegisterRule(rule);
                    Utils.allRules.Add(rule);
                }
            }
        }

        private void RegisterRule(Rule rule)
        {
            Utils.app.WriteJournalComment(Utils.PRODUCT_NAME + " Registering rule " + rule.ToString(), true);
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
                                    Utils.LogError($"{rule.CustomCode} compilation error: {error.GetMessage()}");
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
                        var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(q => q.CodeBase.ToLower().Contains("revitapi.dll"));
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
                Utils.LogError($"Cannot add trigger for rule: {rule} because of exception {ex.Message}");
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
                catch (Exception ex)
                {
                }
                rule.FailureId = failureId;
            }
            else // https://forums.autodesk.com/t5/revit-ideas/api-allow-failuredefinition-createfailuredefinition-during/idi-p/12544647
            {
                rule.FailureId = genericFailureId;
            }
            Utils.app.WriteJournalComment(Utils.PRODUCT_NAME + " Completed registering rule " + rule.ToString(), true);
        }

     
        private void GetParameterPacks()
        {
            var file = Path.Combine(ADDINS_FOLDER, PARAMETER_PACK_FILE_NAME);
            if (!File.Exists(file))
                return;
            var json = File.ReadAllText(file);
            Utils.parameterUIData = JsonConvert.DeserializeObject<ParameterUIData>(json);
        }

        private List<Rule> GetRules()
        {
            var ret = new List<Rule>();
            var file = Path.Combine(ADDINS_FOLDER, RULE_FILE_NAME);
            if (File.Exists(file))
            {
                var markdown = File.ReadAllText(file);
                MarkdownDocument document = Markdown.Parse(markdown);
                var descendents = document.Descendants();
                var codeblocks = document.Descendants<FencedCodeBlock>().ToList();
                foreach (var block in codeblocks)
                {
                    var lines = block.Lines.Cast<StringLine>().Select(q => q.ToString()).ToList();
                    var json = string.Join(string.Empty, lines.Where(q => !q.StartsWith("//")).ToList());
                    RuleData rules = null;
                    try
                    {
                        rules = JsonConvert.DeserializeObject<RuleData>(json);
                    }
                    catch (Exception ex)
                    {
                    }
                    foreach (var rule in rules.Rules)
                    {
                        ret.Add(rule);
                    }
                }

                //for (var row = 2; row < sheet.Dimension.End.Row + 1; row++)
                //    {
                //        try
                //        {
                //            var categoryString = GetCellString(sheet.Cells[row, 1].Value);
                //            if (categoryString == string.Empty)
                //                break;

                //            var rule = new Rule
                //            {
                //                PackName = GetCellString(sheet.Cells[row, 2].Value),
                //                RuleData = GetCellString(sheet.Cells[row, 4].Value),
                //                UserMessage = GetCellString(sheet.Cells[row, 6].Value),
                //            };

                //            if (GetCellString(sheet.Cells[row, 5].Value).ToLower().StartsWith("y"))
                //            {
                //                rule.IsRequired = true;
                //            }
                //            else
                //            {
                //                rule.IsRequired = false;
                //            }

                //            var categories = new List<string>();
                //            foreach (var cat in categoryString.Split(Utils.LIST_SEP))
                //            {
                //                categories.Add(cat);
                //            }
                //            rule.Categories = categories;

                //            var ruleTypeString = GetCellString(sheet.Cells[row, 3].Value);
                //            if (Enum.TryParse(ruleTypeString, out RuleType ruleType))
                //            {
                //                rule.RuleType = ruleType;
                //            }
                //            else
                //            {
                //                Utils.LogError($"Invalid rule type: {ruleTypeString}");
                //            }
                //            ret.Add(rule);
                //        }
                //        catch (Exception ex)
                //        {
                //            Utils.LogError($"Exception loading rule on row {row} {ex.Message}");
                //        }

                //}
            }
            else
            {
                Utils.LogError("File not found: " + file);
            }
            return ret;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}