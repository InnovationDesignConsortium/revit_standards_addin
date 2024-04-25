using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Flee.PublicTypes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RevitDataValidator
{
    public class DataValidationUpdater : IUpdater
    {
        private static readonly string PARAMETER_PARSE_PATTERN = "\\{(.*?)\\}";
        private static readonly string PARAMETER_PARSE_START = "{";
        private static readonly string PARAMETER_PARSE_END = "}";

        private UpdaterId updaterId;

        public DataValidationUpdater(AddInId id)
        {
            updaterId = new UpdaterId(id, new Guid("F1FAF6B3-4C06-42d4-97C1-D2B1EB593EFF"));
        }

        public void Execute(UpdaterData data)
        {
            try
            {
                if (Utils.dialogIdShowing == "Dialog_Revit_PartitionsEnable")
                    return;

                Document doc = data.GetDocument();
                List<ElementId> ids = data.GetModifiedElementIds().ToList();
                List<ElementId> addedAndModifiedIds = data.GetAddedElementIds().ToList();
                addedAndModifiedIds.AddRange(data.GetModifiedElementIds());

                foreach (var rule in Utils.allWorksetRules)
                {
                    if (rule.RevitFileNames != null &&
                        rule.RevitFileNames.FirstOrDefault() != Utils.ALL &&
                        rule.RevitFileNames.Contains(doc.PathName))
                    {
                        continue;
                    }

                    var workset = new FilteredWorksetCollector(Utils.doc).FirstOrDefault(q => q.Name == rule.Workset);
                    if (workset == null)
                        continue;

                    foreach (ElementId id in addedAndModifiedIds)
                    {
                        var element = doc.GetElement(id);

                        if (element is ElementType ||
                            element.Category == null ||
                            rule.Categories == null ||
                            (rule.Categories.First() != Utils.ALL &&
                            !Utils.GetBuiltInCats(rule).Select(q => (int)q).Contains(element.Category.Id.IntegerValue)))
                        {
                            continue;
                        }

                        bool pass = true;
                        foreach (var p in rule.Parameters)
                        {
                            var parameter = GetParameterFromElementOrHostOrType(element, p.Name);
                            if (parameter == null)
                                continue;

                            var paramValue = GetParamAsString(parameter);
                            if (paramValue != p.Value)
                            {
                                pass = false;
                                break;
                            }
                        }

                        if (pass)
                            element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).Set(workset.Id.IntegerValue);
                    }
                }

                var applicableParameterRules = Utils.allParameterRules.Where(rule => rule.RevitFileNames == null ||
                        rule.RevitFileNames.FirstOrDefault() == Utils.ALL ||
                        rule.RevitFileNames.Contains(doc.PathName));

                foreach (var rule in applicableParameterRules.Where(q => q.CustomCode != null && Utils.dictCustomCode.ContainsKey(q.CustomCode)))
                {
                    Type type = Utils.dictCustomCode[rule.CustomCode];
                    object obj = Activator.CreateInstance(type);
                    object x = type.InvokeMember("Run",
                                        BindingFlags.Default | BindingFlags.InvokeMethod,
                                        null,
                                        obj,
                                        new object[] { Utils.doc });
                    if (x is List<ElementId> failureIds)
                    {
                        FailureMessage failureMessage = new FailureMessage(rule.FailureId);
                        failureMessage.SetFailingElements(failureIds);
                        doc.PostFailure(failureMessage);
                    }
                }

                foreach (ElementId id in ids)
                {
                    foreach (var rule in applicableParameterRules)
                    {
                        var element = doc.GetElement(id);

                        if (element.Category == null ||
                            (rule.Categories == null && rule.ElementClasses == null) ||
                            (rule.ElementClasses != null && !rule.ElementClasses.Any(q => q.EndsWith(element.GetType().Name))) ||
                            (rule.Categories != null && rule.Categories.FirstOrDefault() != Utils.ALL &&
                            !Utils.GetBuiltInCats(rule).Select(q => (int)q).Contains(element.Category.Id.IntegerValue)))
                        {
                            continue;
                        }

                        var parameter = element.LookupParameter(rule.ParameterName);
                        if (parameter == null)
                            continue;

                        var paramString = GetParamAsString(parameter);

                        if (paramString == null)
                        {
                            continue;
                        }

                        if (rule.KeyValues != null ||
                            rule.ListOptions != null)
                        {
                            List<string> validValues;
                            if (rule.ListOptions == null)
                            {
                                validValues = rule.KeyValues.Select(q => q.First()).ToList();
                            }
                            else
                            {
                                validValues = rule.ListOptions.Select(q => q.Name).ToList();
                            }
                            if (paramString == null ||
                                !validValues.Contains(paramString))
                            {
                                using (var form = new FormSelectFromList(validValues, rule.UserMessage))
                                {
                                    form.ShowDialog();
                                    var v = form.GetValue();
                                    SetParam(parameter, v);
                                }
                            }
                            else
                            {
                                var keys = rule.KeyValues.FirstOrDefault(q => q[0] == paramString);
                                for (var i = 0; i < rule.DrivenParameters.Count(); i++)
                                {
                                    var drivenParam = element.LookupParameter(rule.DrivenParameters[i]);
                                    if (drivenParam == null)
                                        continue;
                                    SetParam(drivenParam, keys[i + 1]);
                                }
                            }
                        }
                        else if (rule.Format != null)
                        {
                            var formattedString = BuildFormattedString(element, rule.Format, true);
                            if (!paramString.StartsWith(formattedString))
                            {
                                var td = new TaskDialog("Alert")
                                {
                                    MainInstruction =
                                    $"{rule.ParameterName} does not match the required format {rule.Format} and will be renamed to {formattedString}",
                                    CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel
                                };
                                if (td.Show() == TaskDialogResult.Ok)
                                {
                                    if (parameter.Definition.Name == "Type Name")
                                    {
                                        Type t = element.GetType();
                                        var i = 0;
                                        var suffix = string.Empty;

                                        while (new FilteredElementCollector(doc)
                                            .OfClass(t).FirstOrDefault(q => q.Name == formattedString + suffix) != null)
                                        {
                                            i++;
                                            suffix = " " + i.ToString();
                                        }
                                        element.Name = formattedString + suffix;
                                    }
                                    else
                                    {
                                        parameter.Set(formattedString);
                                    }
                                }
                                else
                                {
                                    PostFailure(doc, element.Id, rule.FailureId);
                                }
                            }
                        }
                        else if (rule.Requirement != null)
                        {
                            if (rule.Requirement.StartsWith("IF "))
                            {
                                var thenIdx = rule.Requirement.IndexOf("THEN ");

                                var ifClause = rule.Requirement.Substring("IF ".Length, thenIdx - "IF ".Length - 1);
                                var thenClause = rule.Requirement.Substring(thenIdx + "THEN ".Length);

                                var ifExp = BuildExpressionString(element, ifClause);
                                var result = CSharpScript.EvaluateAsync<bool>(ifExp,
                                     Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                                     .WithImports("System")
                                     ).Result;

                                if (result)
                                {
                                    var thenExp = BuildExpressionString(element, thenClause);
                                }
                            }
                            else
                            {
                                var expressionString = BuildExpressionString(element, rule.Requirement);
                                var exp = paramString + expressionString;
                                var context = new ExpressionContext();
                                var e = context.CompileGeneric<bool>(exp);
                                var result = e.Evaluate();
                                if (!result)
                                {
                                    using (var form = new FormEnterValue(rule.UserMessage + " but current evaluation is " + exp, null))
                                    {
                                        form.ShowDialog();
                                        var v = form.GetValue();
                                        SetParam(parameter, v);
                                    }
                                }
                            }
                        }
                        else if (rule.Formula != null)
                        {
                            var exp = BuildExpressionString(element, rule.Formula);
                            var context = new ExpressionContext();
                            var e = context.CompileGeneric<double>(exp);
                            var result = e.Evaluate();
                            SetParam(parameter, result.ToString());
                        }
                        else if (
                            rule.Regex != null &&
                            paramString != null)
                        {
                            if (paramString == null ||
                                !Regex.IsMatch(paramString, rule.Regex))
                            {
                                using (var form = new FormEnterValue(rule.UserMessage, rule.Regex))
                                {
                                    form.ShowDialog();
                                    var v = form.GetValue();
                                    SetParam(parameter, v);
                                    Utils.propertiesPanel.Refresh();
                                }
                            }
                        }
                        else if (rule.PreventDuplicates != null)
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
                                using (var form = new FormEnterValue(rule.UserMessage, null))
                                {
                                    form.ShowDialog();
                                    var v = form.GetValue();
                                    SetParam(parameter, v);
                                    Utils.propertiesPanel.Refresh();
                                }
                            }
                        }
                        else if (rule.FromHostInstance != null)
                        {
                            if (element is FamilyInstance fi)
                            {
                                var host = fi.Host;
                                if (host != null)
                                {
                                    var value = GetParamAsValueString(host.LookupParameter(rule.FromHostInstance));
                                    if ((value ?? string.Empty) != (paramString ?? string.Empty))
                                    {
                                        SetParam(parameter, value);
                                        TaskDialog.Show("ParameterRule", $"{rule.UserMessage}");
                                    }
                                }
                            }
                            else if (element is HostObject host)
                            {
                                var value = GetParamAsValueString(host.LookupParameter(rule.FromHostInstance));
                                var inserts = new FilteredElementCollector(doc)
                                    .OfClass(typeof(FamilyInstance))
                                    .Cast<FamilyInstance>()
                                    .Where(q => q.Host != null && q.Host.Id == host.Id);
                                foreach (var insert in inserts)
                                {
                                    SetParam(insert.LookupParameter(rule.FromHostInstance), value);
                                }
                            }
                        }
                        else
                        {
                            Utils.errors.Add($"Not Implmented");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogException("DataValidationUpdater", ex);
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

        private static string RemoveIllegalCharacters(string s)
        {
            char[] illegal = { '\\', ':', '{', '}', '[', ']', '|', '>', '<', '~', '?', '`', ';', };
            return string.Concat(s.Split(illegal));
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
            else if (p.StorageType == StorageType.ElementId)
            {
                return p.AsValueString();
            }

            return null;
        }

        //private static void ParseAndSetParameter(ParameterRule rule, Element element)
        //{
        //    var elementOrHostOrHostType = element;
        //    if (element is FamilyInstance fi)
        //    {
        //        var host = fi.Host;
        //        if (rule.RuleType == RuleType.FromHostInstance)
        //        {
        //            elementOrHostOrHostType = host;
        //        }
        //        else if (rule.RuleType == RuleType.FromHostType)
        //        {
        //            elementOrHostOrHostType = element.Document.GetElement(host.GetTypeId());
        //        }
        //    }
        //    var matches = Regex.Matches(rule.RuleData, PARAMETER_PARSE_PATTERN);

        //    var parameter = element.LookupParameter(rule.PackName);
        //    if (parameter == null)
        //    {
        //        return;
        //    }

        //    var s = string.Empty;
        //    for (int i = 0; i < matches.Count; i++)
        //    {
        //        var match = matches[i];
        //        var matchValueCleaned = match.Value.Replace(PARAMETER_PARSE_START, string.Empty).Replace(PARAMETER_PARSE_END, string.Empty);
        //        var matchEnd = match.Index + match.Length;

        //        string paramValue;
        //        if (parameter.StorageType == StorageType.String)
        //        {
        //            paramValue = GetParamAsValueString(elementOrHostOrHostType.LookupParameter(matchValueCleaned));
        //        }
        //        else
        //        {
        //            paramValue = GetParamAsDoubleString(elementOrHostOrHostType.LookupParameter(matchValueCleaned));
        //        }

        //        s += paramValue;
        //        if (i == matches.Count - 1)
        //        {
        //            s += rule.RuleData.Substring(matchEnd);
        //        }
        //        else
        //        {
        //            s += GetStringAfterParsedParameterName(rule, matchEnd, matches[i + 1].Index);
        //        }
        //    }

        //    if (parameter.StorageType == StorageType.Double)
        //    {
        //        var expression = new Expression(s);
        //        s = expression.calculate().ToString();
        //    }
        //    SetParam(parameter, s);
        //}

        private static string GetStringAfterParsedParameterName(string input, int matchEnd, int nextMatchIndex)
        {
            var length = nextMatchIndex - matchEnd;
            return input.Substring(matchEnd, length);
        }

        private string BuildFormattedString(Element element, string input, bool removeIllegalCharacters)
        {
            var matches = Regex.Matches(input, PARAMETER_PARSE_PATTERN);

            var s = string.Empty;
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var matchValueCleaned = match.Value.Replace(PARAMETER_PARSE_START, string.Empty).Replace(PARAMETER_PARSE_END, string.Empty);
                var matchEnd = match.Index + match.Length;
                if (s == string.Empty)
                    s += input.Substring(0, match.Index);
                var parameter = GetParameterFromElementOrHostOrType(element, matchValueCleaned);
                if (parameter != null)
                {
                    if (parameter.StorageType == StorageType.Double)
                    {
                        double paramValue = GetParamAsDouble(parameter);
                        var options = new FormatValueOptions
                        {
                            AppendUnitSymbol = true
                        };
                        var formatted = UnitFormatUtils.Format(element.Document.GetUnits(), parameter.Definition.GetDataType(), paramValue, false, options);
                        s += formatted;
                    }
                    else if (parameter.StorageType == StorageType.Integer)
                    {
                        if (parameter.AsValueString() == parameter.AsInteger().ToString())
                        {
                            s += parameter.AsInteger();
                        }
                        else
                        {
                            if (parameter.GetTypeId() == ParameterTypeId.FunctionParam)
                            {
                                s += ((WallFunction)parameter.AsInteger()).ToString();
                            }
                        }
                    }
                    else if (parameter.StorageType == StorageType.String)
                    {
                        s += parameter.AsString();
                    }
                    else if (parameter.StorageType == StorageType.ElementId)
                    {
                        s += parameter.AsValueString();
                    }
                }

                if (i == matches.Count - 1)
                {
                    s += input.Substring(matchEnd);
                }
                else
                {
                    s += GetStringAfterParsedParameterName(input, matchEnd, matches[i + 1].Index);
                }
            }
            if (removeIllegalCharacters)
                s = RemoveIllegalCharacters(s);

            return s;
        }

        private string BuildExpressionString(Element element, string input)
        {
            var matches = Regex.Matches(input, PARAMETER_PARSE_PATTERN);

            var s = string.Empty;
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var matchValueCleaned = match.Value.Replace(PARAMETER_PARSE_START, string.Empty).Replace(PARAMETER_PARSE_END, string.Empty);
                var matchEnd = match.Index + match.Length;
                if (s == string.Empty)
                    s += input.Substring(0, match.Index);
                var parameter = GetParameterFromElementOrHostOrType(element, matchValueCleaned);
                if (parameter != null)
                {
                    if (parameter.StorageType == StorageType.Integer || parameter.StorageType == StorageType.Double)
                    {
                        double paramValue = GetParamAsDouble(parameter);
                        s += paramValue;
                    }
                    else if (parameter.StorageType == StorageType.String)
                    {
                        s += "\"" + parameter.AsString() + "\"";
                    }
                    else if (parameter.StorageType == StorageType.ElementId)
                    {
                        s += "\"" + parameter.AsValueString() + "\"";
                    }
                }

                if (i == matches.Count - 1)
                {
                    s += input.Substring(matchEnd);
                }
                else
                {
                    s += GetStringAfterParsedParameterName(input, matchEnd, matches[i + 1].Index);
                }
            }
            return s;
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

        private Parameter GetParameterFromElementOrHostOrType(Element e, string paramName)
        {
            var p = e.LookupParameter(paramName);
            if (p != null)
                return p;
            var elementType = e.Document.GetElement(e.GetTypeId());
            if (elementType == null)
                return null;
            p = elementType.LookupParameter(paramName);
            if (p != null)
                return p;
            if (e is FamilyInstance fi)
            {
                p = fi.Host.LookupParameter(paramName);
                if (p != null)
                    return p;
                var hostType = e.Document.GetElement(fi.Host.GetTypeId());
                p = hostType.LookupParameter(paramName);
                if (p != null)
                    return p;
            }
            return null;
        }

        private static double GetParamAsDouble(Parameter p)
        {
            if (p.StorageType == StorageType.Integer)
                return Convert.ToDouble(p.AsInteger());
            if (p.StorageType == StorageType.Double)
                return p.AsDouble();
            return double.NaN;
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
                if (double.IsInfinity(d) ||
                    double.IsNaN(d))
                {
                    p.Set(0);
                }
                else
                {
                    p.Set(d);
                }
            }
        }
    }
}