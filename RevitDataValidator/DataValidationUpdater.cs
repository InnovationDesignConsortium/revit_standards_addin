﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using org.mariuszgromada.math.mxparser;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitDataValidator
{

    public class DataValidationUpdater : IUpdater
    {
        private static readonly string PARAMETER_PARSE_PATTERN = "\\<(.*?)\\>";
        private static readonly string PARAMETER_PARSE_START = "<";
        private static readonly string PARAMETER_PARSE_END = ">";

        private UpdaterId updaterId;

        public DataValidationUpdater(AddInId id)
        {
            updaterId = new UpdaterId(id, new Guid("F1FAF6B3-4C06-42d4-97C1-D2B1EB593EFF"));
        }

        public void Execute(UpdaterData data)
        {
            try
            {
                Document doc = data.GetDocument();
                List<ElementId> ids = new List<ElementId>();
                ids.AddRange(data.GetModifiedElementIds());


                foreach (var rule in Utils.allRules)
                {
                    if (rule.DocumentPath != null &&
                        rule.DocumentPath != doc.PathName)
                    {
                        continue;
                    }

                    if (rule.RuleType == RuleType.FromHostInstance)
                    {
                        var idsFromHost = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilyInstance))
                            .Cast<FamilyInstance>()
                            .Where(q => q.Host != null && ids.Contains(q.Host.Id))
                            .Select(q => q.Id)
                            .ToList();
                        ids.AddRange(idsFromHost);
                    }
                    else if (rule.RuleType == RuleType.FromHostType)
                    {
                        var hostTypeIds = new FilteredElementCollector(doc, ids)
                            .OfClass(typeof(HostObjAttributes))
                            .ToElementIds()
                            .ToList();

                        var hostIds = new FilteredElementCollector(doc)
                            .OfClass(typeof(HostObject))
                            .Cast<HostObject>()
                            .Where(q => hostTypeIds.Contains(q.GetTypeId()))
                            .Select(q => q.Id)
                            .ToList();

                        var idsFromHostType = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilyInstance))
                            .Cast<FamilyInstance>()
                            .Where(q => q.Host != null && hostIds.Contains(q.Host.Id))
                            .Select(q => q.Id)
                            .ToList();

                        ids = idsFromHostType;
                    }

                    foreach (ElementId id in ids)
                    {
                        var element = doc.GetElement(id);

                        if (element.Category != null &&
                            ((rule.Categories.Count() == 1 && rule.Categories.First() == Utils.ALL) ||
                            Utils.GetBuiltInCats(rule).Select(q => (int)q).Contains(element.Category.Id.IntegerValue)))
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
                                var validValues = rule.RuleData.Split(Utils.LIST_SEP).ToList();
                                if (paramString == null ||
                                    !validValues.Contains(paramString))
                                {
                                    using (var form = new FormSelectFromList(validValues, rule.UserMessage))
                                    {
                                        form.ShowDialog();
                                        var v = form.GetValue();
                                        SetParam(parameter, v);
                                    }
                                   // PostFailure(doc, id, rule.FailureId);
                                }
                            }
                            else if (rule.RuleType == RuleType.Regex)
                            {
                                if (paramString == null ||
                                    !Regex.IsMatch(paramString, rule.RuleData))
                                {
                                    using (var form = new FormEnterValue(rule.UserMessage, rule.RuleData))
                                    {
                                        form.ShowDialog();
                                        var v = form.GetValue();
                                        SetParam(parameter, v);
                                    }

                                    //PostFailure(doc, id, rule.FailureId);
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
                            else if (rule.RuleType == RuleType.FromHostType ||
                                rule.RuleType == RuleType.FromHostInstance ||
                                rule.RuleType == RuleType.Calculated)
                            {
                                ParseAndSetParameter(rule, element);
                            }
                            else
                            {
                                Utils.errors.Add($"Not Implmented for {rule.RuleType}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog td = new TaskDialog("Error")
                {
                    MainInstruction = ex.Message,
                    MainContent = ex.StackTrace
                };
                td.Show();
            }
        }

        public string GetAdditionalInformation()
        { return "DataValidationUpdater"; }

        public ChangePriority GetChangePriority()
        { return ChangePriority.FloorsRoofsStructuralWalls; }

        public UpdaterId GetUpdaterId()
        { return updaterId; }

        public string GetUpdaterName()
        { return "DataValidationUpdater"; }

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

        private static void ParseAndSetParameter(Rule rule, Element element)
        {
            var elementOrHostOrHostType = element;
            if (element is FamilyInstance fi)
            {
                var host = fi.Host;
                if (rule.RuleType == RuleType.FromHostInstance)
                {
                    elementOrHostOrHostType = host;
                }
                else if (rule.RuleType == RuleType.FromHostType)
                {
                    elementOrHostOrHostType = element.Document.GetElement(host.GetTypeId());
                }
            }
            var matches = Regex.Matches(rule.RuleData, PARAMETER_PARSE_PATTERN);

            var parameter = element.LookupParameter(rule.ParameterName);
            if (parameter == null)
            {
                return;
            }

            var s = string.Empty;
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var matchValueCleaned = match.Value.Replace(PARAMETER_PARSE_START, string.Empty).Replace(PARAMETER_PARSE_END, string.Empty);
                var matchEnd = match.Index + match.Length;

                string paramValue;
                if (parameter.StorageType == StorageType.String)
                {
                    paramValue = GetParamAsValueString(elementOrHostOrHostType.LookupParameter(matchValueCleaned));
                }
                else 
                {
                    paramValue = GetParamAsDoubleString(elementOrHostOrHostType.LookupParameter(matchValueCleaned));
                }   

                s += paramValue;
                if (i == matches.Count - 1)
                {
                    s += rule.RuleData.Substring(matchEnd);
                }
                else
                {
                    s += GetStringAfterParsedParameterName(rule, matchEnd, matches[i + 1].Index);
                }
            }

            if (parameter.StorageType == StorageType.Double)
            {
                var expression = new Expression(s);
                s = expression.calculate().ToString();
            }

            SetParam(parameter, s);
        }

        private static string GetStringAfterParsedParameterName(Rule rule, int matchEnd, int nextMatchIndex)
        {
            var length = nextMatchIndex - matchEnd;
            return rule.RuleData.Substring(matchEnd, length);
        }

        private static void PostFailure(Document doc, ElementId id, FailureDefinitionId failureId)
        {
            FailureMessage failureMessage = new FailureMessage(failureId);
            failureMessage.SetFailingElement(id);
            doc.PostFailure(failureMessage);
        }

        private static string GetParamAsValueString(Parameter p)
        {
            if (p == null)
                return null;
            return p.AsValueString();
        }

        private static string GetParamAsDoubleString(Parameter p)
        {
            if (p == null)
                return null;
            return p.AsDouble().ToString();
        }

        private static void SetParam(Parameter p, string s)
        {
            if (p == null)
                return;
            if (p.StorageType == StorageType.String)
            {
                p.Set(s);
            }
            else if (p.StorageType == StorageType.Integer)
            {
                if (int.TryParse(s, out int sInt))
                {
                    p.Set(sInt);
                }
                else if (s == "No")
                {
                    p.Set(0);
                }
                else if (s == "Yes")
                {
                    p.Set(1);
                }
            }
            else if (p.StorageType == StorageType.Double &&
                double.TryParse(s, out double d))
            {
                if (!double.IsInfinity(d) &&
                    !double.IsNaN(d))
                {
                    p.Set(d);
                }
            }
        }
    }
}