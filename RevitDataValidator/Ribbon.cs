﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Calculated on Instance - Room Occupany * some other parameter = value of this parameter
// Calculated from Context - Door from Wall Rating
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
        private static readonly char LIST_SEP = ',';
        private static readonly string ALL = "<all>";
        private AddInId applicationId;
        private static Dictionary<string, BuiltInCategory> catMap = new Dictionary<string, BuiltInCategory>();
        private UpdaterId updaterId;
        private static List<Rule> allRules;
        private static List<string> errors;

        public enum RuleType
        {
            List,
            Regex,
            PreventDuplicates,
            FromHostType
        }

        private class Rule
        {
            public List<string> Categories { get; set; }
            public string ParameterName { get; set; }
            public RuleType RuleType { get; set; }
            public string RuleData { get; set; }
            public string UserMessage { get; set; }
            public FailureDefinitionId FailureId { get; set; }
            public string DocumentPath { get; set; }
            public bool IsRequired { get; set; }
            public override string ToString()
            {
                return ParameterName + " " + RuleType + " " + RuleData;
            }
        }

        public Result OnStartup(UIControlledApplication application)
        {
            errors = new List<string>();
            allRules = new List<Rule>();
            applicationId = application.ActiveAddInId;
            application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;

            foreach (BuiltInCategory bic in Enum.GetValues(typeof(BuiltInCategory)))
            {
                try
                {
                    catMap.Add(LabelUtils.GetLabelFor(bic), bic);
                }
                catch
                { }
            }

            MyUpdater myUpdater = new MyUpdater(applicationId);
            updaterId = myUpdater.GetUpdaterId();

            UpdaterRegistry.RegisterUpdater(myUpdater, true);

            genericFailureId = new FailureDefinitionId(Guid.NewGuid());
            FailureDefinition.CreateFailureDefinition(
                genericFailureId,
                FailureSeverity.Error,
                RULE_DEFAULT_MESSAGE);

            var file = Path.Combine(ADDINS_FOLDER, RULE_FILE_NAME);
            var rules = GetRules(file);

            RegisterRules(rules, null);

            if (errors.Any())
            {
                var errorfile = Path.Combine(Path.GetDirectoryName(Path.GetTempPath()), @"..\RevitValidator-ErrorLog-" + DateTime.Now.ToString().Replace(":", "-").Replace("/", "_") + ".txt");
                using (StreamWriter sw = new StreamWriter(errorfile, true))
                {
                    sw.Write(string.Join(Environment.NewLine, errors));
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
                allRules.Add(rule);
            }
        }

        private static List<BuiltInCategory> GetBuiltInCats(Rule rule)
        {
            if (rule.Categories.Count() == 1 && rule.Categories.First() == ALL)
            {
                return catMap.Values.ToList();
            }
            else
            {
                return rule.Categories.Select(q => catMap[q]).ToList();
            }
        }

        private void RegisterRule(Rule rule, string docPath)
        {
            try
            {
                UpdaterRegistry.AddTrigger(
                    updaterId,
                    new ElementMulticategoryFilter(GetBuiltInCats(rule)),
                    Element.GetChangeTypeAny());
            }
            catch
            {
                errors.Add("Cannot add trigger for rule: " + rule.ToString());
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
                    FailureSeverity.Warning,
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
                            foreach (var cat in categoryString.Split(LIST_SEP))
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
                                errors.Add("Invalid rule type: " + ruleTypeString);
                            }
                            ret.Add(rule);
                        }
                        catch (Exception ex)
                        {
                            errors.Add("Exception loading rule on row " + row + " " + ex.Message);
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
            var file = Path.GetFileNameWithoutExtension(path);
            var rules = GetRules(Path.Combine(folder, file + RULE_FILE_SEP + RULE_FILE_NAME));
            RegisterRules(rules, path);
        }

        public class MyUpdater : IUpdater
        {
            private UpdaterId updaterId;

            public MyUpdater(AddInId id)
            {
                updaterId = new UpdaterId(id, new Guid("F1FAF6B3-4C06-42d4-97C1-D2B1EB593EFF"));
            }

            public void Execute(UpdaterData data)
            {
                Document doc = data.GetDocument();
                List<ElementId> ids = new List<ElementId>();
                ids.AddRange(data.GetModifiedElementIds());
                foreach (var rule in allRules)
                {
                    if (rule.DocumentPath != null &&
                        rule.DocumentPath != doc.PathName)
                    {
                        continue;
                    }

                    foreach (ElementId id in ids)
                    {
                        var element = doc.GetElement(id);

                        if (element.Category != null &&
                            ((rule.Categories.Count() == 1 && rule.Categories.First() == ALL) ||
                            GetBuiltInCats(rule).Select(q => (int)q).Contains(element.Category.Id.IntegerValue)))
                        {
                            var parameter = element.LookupParameter(rule.ParameterName);
                            if (parameter == null)
                                continue;

                            var paramString = GetParamAsString(parameter);

                            if (!rule.IsRequired && paramString == null)
                            {
                                continue;
                            }

                            if (rule.RuleType == RuleType.List)
                            {
                                if (paramString == null ||
                                    !rule.RuleData.Split(LIST_SEP).ToList().Contains(paramString))
                                {
                                    PostFailure(doc, id, rule.FailureId);
                                }
                            }
                            else if (rule.RuleType == RuleType.Regex)
                            {
                                if (paramString == null ||
                                    !Regex.IsMatch(paramString, rule.RuleData))
                                {
                                    PostFailure(doc, id, rule.FailureId);
                                }
                            }
                            else if (rule.RuleType == RuleType.PreventDuplicates)
                            {
                                var bic = (BuiltInCategory)element.Category.Id.IntegerValue;
                                var others = new FilteredElementCollector(doc)
                                    .OfCategory(bic)
                                    .WhereElementIsNotElementType()
                                    .Where(q => q.Id != element.Id);
                                List<string> othersParams =
                                    others.Select(q => GetParamAsString(q.LookupParameter(rule.ParameterName))).ToList();
                                if (othersParams.Contains(paramString))
                                {
                                    PostFailure(doc, id, rule.FailureId);
                                }
                            }
                            else if (rule.RuleType == RuleType.FromHostType)
                            {
                                if (element is FamilyInstance fi)
                                {
                                    var host = fi.Host;
                                    var hostType = doc.GetElement(host.GetTypeId());
                                    var pattern = "\\<(.*?)\\>";
                                    var matches = Regex.Matches(rule.RuleData, pattern);
                                    var s = "";

                                    for (int i = 0; i < matches.Count; i++)
                                    {
                                        var match = matches[i];
                                        var matchValueCleaned = match.Value.Replace("<", "").Replace(">", "");
                                        var paramValue = GetParamAsValueString(
                                            hostType.LookupParameter(matchValueCleaned));
                                        s += paramValue;
                                        var matchEnd = match.Index + match.Length;
                                        if (i == matches.Count - 1)
                                        {
                                            s += rule.RuleData.Substring(matchEnd);
                                        }
                                        else
                                        {
                                            var nextMatch = matches[i + 1];
                                            s += rule.RuleData.Substring(matchEnd, rule.RuleData.Length - matchEnd - nextMatch.Index - 2);
                                        }
                                    }
                                    SetParamAsString(element.LookupParameter(rule.ParameterName), s);
                                }
                            }
                            else
                            {
                                errors.Add("Not Implmented for " + rule.RuleType.ToString());
                            }
                        }
                    }
                }
            }

            public string GetAdditionalInformation()
            { return "MyUpdater"; }

            public ChangePriority GetChangePriority()
            { return ChangePriority.FloorsRoofsStructuralWalls; }

            public UpdaterId GetUpdaterId()
            { return updaterId; }

            public string GetUpdaterName()
            { return "MyUpdater"; }
        }

        private static void PostFailure(Document doc, ElementId id, FailureDefinitionId failureId)
        {
            FailureMessage failureMessage = new FailureMessage(failureId);
            failureMessage.SetFailingElement(id);
            doc.PostFailure(failureMessage);
        }

        private static void SetParamAsString(Parameter p, string s)
        {
            if (p == null)
                return;
            if (p.StorageType != StorageType.String)
                return;
            p.Set(s);
        }

        private static string GetParamAsValueString(Parameter p)
        {
            if (p == null)
                return null;
            return p.AsValueString();
        }

        private static string GetParamAsString(Parameter p)
        {
            if (p == null)
                return null;

            if (p.StorageType == StorageType.String)
            {
                if (p.AsString() == null)
                    return null;

                return p.AsString();
            }
            else if (p.StorageType == StorageType.Double)
            {
                return p.AsDouble().ToString();
            }
            else if (p.StorageType == StorageType.Integer)
            {
                return p.AsInteger().ToString();
            }

            return null;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}