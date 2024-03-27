using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace RevitDataValidator
{
    internal class Ribbon : IExternalApplication
    {
        private static FailureDefinitionId failureId;
        private static FailureDefinitionId genericFailureId;
        private readonly string ADDINS_FOLDER = @"C:\ProgramData\Autodesk\Revit\Addins";
        private readonly string RULE_FILE_NAME = "projectrules.md";
        private readonly string PARAMETER_PACK_FILE_NAME = "ParameterPacks.json";
        private readonly string RULE_DEFAULT_MESSAGE = "This value is not allowed";
        private AddInId applicationId;
        public static UpdaterId updaterId;


        public Result OnStartup(UIControlledApplication application)
        {
            Utils.app = application.ControlledApplication;
            Utils.errors = new List<string>();
            Utils.allRules = new List<Rule>();
            applicationId = application.ActiveAddInId;
            application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;
            Utils.eventHandlerWithProperty = new EventHandlerWithProperty();
            Utils.eventHandlerWithParameterValue = new EventHandlerWithParameterValue();

            Utils.paneId = new DockablePaneId(Guid.NewGuid());
            GetParameterPacks();
            Utils.propertiesPanel = new PropertiesPanel();

            application.RegisterDockablePane(Utils.paneId, "Properties Panel", Utils.propertiesPanel as IDockablePaneProvider);
            application.ViewActivated += HideDockablePanelOnStartup;
            application.SelectionChanged += Application_SelectionChanged;

            foreach (BuiltInCategory bic in Enum.GetValues(typeof(BuiltInCategory)))
            {
                try
                {
                    Utils.catMap.Add(LabelUtils.GetLabelFor(bic), bic);
                }
                catch
                { }
            }

            DataValidationUpdater dataValidationUpdater = new DataValidationUpdater(applicationId);
            updaterId = dataValidationUpdater.GetUpdaterId();
            UpdaterRegistry.RegisterUpdater(dataValidationUpdater, true);

            genericFailureId = new FailureDefinitionId(Guid.NewGuid());
            FailureDefinition.CreateFailureDefinition(
                genericFailureId,
                FailureSeverity.Error,
                RULE_DEFAULT_MESSAGE);

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

            if (e.GetSelectedElements().Count() == 0)
            {
                pane.Hide();
                return;
            }

            Utils.selectedIds = e.GetSelectedElements();
            pane.Show();
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);
        public struct POINT
        {
            public int X;
            public int Y;
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
            RegisterRules(e.Document.PathName);
        }

        public void RegisterRules(string filename)
        {
            UpdaterRegistry.RemoveAllTriggers(updaterId);
            Utils.allRules.Clear();

            var rules = GetRules();
            foreach (var rule in rules.Where(q =>
                q.RevitFileNames == null ||
                q.RevitFileNames.Contains(Path.GetFileNameWithoutExtension(filename))))
            {
                RegisterRule(rule);
                Utils.allRules.Add(rule);
            }
        }

        private void RegisterRule(Rule rule)
        {
            Utils.app.WriteJournalComment(Utils.PRODUCT_NAME + " Registering rule " + rule.ToString(), true);
            try
            {
                UpdaterRegistry.AddTrigger(
                    updaterId,
                    new ElementMulticategoryFilter(Utils.GetBuiltInCats(rule)),
                    Element.GetChangeTypeAny());
            }
            catch (Exception ex)
            {
                Utils.LogError($"Cannot add trigger for rule: {rule} because of exception {ex.Message}");
            }

            //if (rule.RevitFileNames == null)
            //{
            //    failureId = new FailureDefinitionId(Guid.NewGuid());
            //    var message = "";
            //    if (string.IsNullOrEmpty(message))
            //    {
            //        message = RULE_DEFAULT_MESSAGE;
            //    }
            //    FailureDefinition.CreateFailureDefinition(
            //        failureId,
            //        FailureSeverity.Error,
            //        message);

            //    rule.FailureId = failureId;
            //}
            //else // https://forums.autodesk.com/t5/revit-ideas/api-allow-failuredefinition-createfailuredefinition-during/idi-p/12544647
            //{
            //    rule.FailureId = genericFailureId;
            //}
            Utils.app.WriteJournalComment(Utils.PRODUCT_NAME + " Completed registering rule " + rule.ToString(), true);
        }

        private void GetParameterPacks()
        {
            var ret = new List<Rule>();
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
                //                ParameterName = GetCellString(sheet.Cells[row, 2].Value),
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