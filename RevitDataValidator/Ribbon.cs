using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// Calculated on Instance - Room Occupany * some other parameter = value of this parameter
// TypeCheck - Room From:Name = if Office & Room To:Name=Corridor, then DoorType = Flush1

namespace RevitDataValidator
{
    internal class Ribbon : IExternalApplication
    {
        private static FailureDefinitionId failureId;
        private static FailureDefinitionId genericFailureId;
        private readonly string ADDINS_FOLDER = @"C:\ProgramData\Autodesk\Revit\Addins";
        private readonly string RULE_FILE_NAME = "DataValidationRules.xlsx";
        private readonly string RULE_FILE_SEP = "-";
        private readonly string RULE_DEFAULT_MESSAGE = "This value is not allowed";
        private AddInId applicationId;
        private UpdaterId updaterId;

        public Result OnStartup(UIControlledApplication application)
        {
            Utils.errors = new List<string>();
            Utils.allRules = new List<Rule>();
            applicationId = application.ActiveAddInId;
            application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;

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

            var file = Path.Combine(ADDINS_FOLDER, RULE_FILE_NAME);
            var rules = GetRules(file);

            RegisterRules(rules, null);

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

        private void RegisterRules(List<Rule> rules, string docPath)
        {
            foreach (var rule in rules)
            {
                RegisterRule(rule, docPath);
                Utils.allRules.Add(rule);
            }
        }

        private void RegisterRule(Rule rule, string docPath)
        {
            try
            {
                UpdaterRegistry.AddTrigger(
                    updaterId,
                    new ElementMulticategoryFilter(Utils.GetBuiltInCats(rule)),
                    Element.GetChangeTypeAny());
            }
            catch (Exception ex)
            {
                Utils.errors.Add($"Cannot add trigger for rule: {rule} because of exception {ex.Message}");
            }

            if (docPath == null)
            {
                failureId = new FailureDefinitionId(Guid.NewGuid());
                var message = rule.UserMessage;
                if (string.IsNullOrEmpty(message))
                {
                    message = RULE_DEFAULT_MESSAGE;
                }
                FailureDefinition.CreateFailureDefinition(
                    failureId,
                    FailureSeverity.Error,
                    message);

                rule.FailureId = failureId;
                rule.DocumentPath = docPath;
            }
            else // https://forums.autodesk.com/t5/revit-ideas/api-allow-failuredefinition-createfailuredefinition-during/idi-p/12544647
            {
                rule.FailureId = genericFailureId;
                rule.DocumentPath = docPath;
            }
        }

        private List<Rule> GetRules(string file)
        {
            var ret = new List<Rule>();
            if (File.Exists(file))
            {
                using (var package = new ExcelPackage(new FileInfo(file)))
                {
                    var sheet = package.Workbook.Worksheets[1];
                    for (var row = 2; row < sheet.Dimension.End.Row + 1; row++)
                    {
                        try
                        {
                            var categoryString = GetCellString(sheet.Cells[row, 1].Value);
                            if (categoryString == string.Empty)
                                break;

                            var rule = new Rule
                            {
                                ParameterName = GetCellString(sheet.Cells[row, 2].Value),
                                RuleData = GetCellString(sheet.Cells[row, 4].Value),
                                UserMessage = GetCellString(sheet.Cells[row, 6].Value),
                            };

                            if (GetCellString(sheet.Cells[row, 5].Value).ToLower().StartsWith("y"))
                            {
                                rule.IsRequired = true;
                            }
                            else
                            {
                                rule.IsRequired = false;
                            }

                            var categories = new List<string>();
                            foreach (var cat in categoryString.Split(Utils.LIST_SEP))
                            {
                                categories.Add(cat);
                            }
                            rule.Categories = categories;

                            var ruleTypeString = GetCellString(sheet.Cells[row, 3].Value);
                            if (Enum.TryParse(ruleTypeString, out RuleType ruleType))
                            {
                                rule.RuleType = ruleType;
                            }
                            else
                            {
                                Utils.errors.Add($"Invalid rule type: {ruleTypeString}");
                            }
                            ret.Add(rule);
                        }
                        catch (Exception ex)
                        {
                            Utils.errors.Add($"Exception loading rule on row {row} {ex.Message}");
                        }
                    }
                }
            }
            return ret;
        }

        private string GetCellString(object range)
        {
            if (range == null)
                return string.Empty;

            return range.ToString();
        }

        private void ControlledApplication_DocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
        {
            var doc = e.Document;
            var path = doc.PathName;
            var folder = Path.GetDirectoryName(path);
            var rvtfilename = Path.GetFileNameWithoutExtension(path);
            var rulefile = Path.Combine(folder, rvtfilename + RULE_FILE_SEP + RULE_FILE_NAME);
            var rules = GetRules(rulefile);
            RegisterRules(rules, path);
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}