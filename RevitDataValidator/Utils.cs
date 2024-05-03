using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Flee.PublicTypes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitDataValidator
{
    public static class Utils
    {
        public static string dialogIdShowing = "";
        public static ControlledApplication app;
        public static string PRODUCT_NAME = "RevitDataValidator";
        public static readonly string ALL = "<all>";
        public static readonly char LIST_SEP = ',';
        public static List<ParameterRule> allParameterRules;
        public static List<WorksetRule> allWorksetRules;
        public static List<string> errors;
        public static PropertiesPanel propertiesPanel;
        public static DockablePaneId paneId;
        public static ParameterUIData parameterUIData;
        public static List<ElementId> selectedIds;
        public static Document doc;
        public static EventHandlerWithParameterObject eventHandlerWithParameterObject;
        public static EventHandlerCreateInstancesInRoom eventHandlerCreateInstancesInRoom;
        public static Dictionary<string, string> dictCategoryPackSet;
        public static Dictionary<string, Type> dictCustomCode;
        private const string PARAMETER_PARSE_PATTERN = "\\{(.*?)\\}";
        private const string PARAMETER_PARSE_START = "{";
        private const string PARAMETER_PARSE_END = "}";
        public static string dllPath;

        private static readonly Dictionary<BuiltInCategory, List<BuiltInCategory>> CatToHostCatMap = new Dictionary<BuiltInCategory, List<BuiltInCategory>>()
    {
        { BuiltInCategory.OST_Doors, new List<BuiltInCategory> {BuiltInCategory.OST_Walls } },
        { BuiltInCategory.OST_Windows, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_Roofs } },
        { BuiltInCategory.OST_Rooms, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_RoomSeparationLines } },
    };

        public static Dictionary<string, BuiltInCategory> catMap = new Dictionary<string, BuiltInCategory>();

        public static List<RuleFailure> GetFailures(ElementId id, List<ParameterString> inputParameterValues, out List<ParameterString> parametersToSet)
        {
            var ret = new List<RuleFailure>();
            var applicableParameterRules = GetApplicableParameterRules();
            parametersToSet = new List<ParameterString>();
            foreach (var rule in applicableParameterRules)
            {
                var element = doc.GetElement(id);

                if (element.Category == null ||
                    (rule.Categories == null && rule.ElementClasses == null) ||
                    (rule.ElementClasses?.Any(q => q.EndsWith(element.GetType().Name)) == false) ||
                    (rule.Categories != null && rule.Categories.FirstOrDefault() != Utils.ALL &&
                    !Utils.GetBuiltInCats(rule).Select(q => (int)q).Contains(element.Category.Id.IntegerValue)))
                {
                    continue;
                }

                var parameter = Utils.GetParameter(element, rule.ParameterName);
                if (parameter == null)
                    continue;

                var parameterValueAsString = GetParamAsString(parameter);
                if (inputParameterValues?.FirstOrDefault(q => q.Parameter.Definition.Name == rule.ParameterName) != null)
                {
                    var parameterStringMatch = inputParameterValues.FirstOrDefault(q => q.Parameter.Definition.Name == rule.ParameterName);
                    parameter = parameterStringMatch.Parameter;
                    parameterValueAsString = parameterStringMatch.Value;
                }
                if (parameterValueAsString == null)
                {
                    continue;
                }

                Utils.Log($"Runing Updater on {Utils.GetElementInfo(element)} for parameter {parameter.Definition.Name}");

                if (rule.KeyValues != null ||
                    rule.ListOptions != null)
                {
                    if (rule.ListOptions != null && (parameterValueAsString == null ||
                        !rule.ListOptions.Select(q => q.Name).Contains(parameterValueAsString)))
                    {
                        ret.Add(new RuleFailure
                        {
                            Rule = rule,
                            ElementId = id,
                            FailureType = FailureType.List
                        });
                    }
                    else if (rule.KeyValues != null)
                    {
                        var keys = rule.KeyValues.FirstOrDefault(q => q[0] == parameterValueAsString);
                        for (var i = 0; i < rule.DrivenParameters.Count(); i++)
                        {
                            var drivenParam = Utils.GetParameter(element, rule.DrivenParameters[i]);
                            if (drivenParam == null)
                                continue;
                            parametersToSet.Add(new ParameterString(drivenParam, keys[i + 1]));
                        }
                    }
                }
                else if (rule.Format != null)
                {
                    var formattedString = BuildFormattedString(element, rule.Format, true);
                    if (!parameterValueAsString.StartsWith(formattedString))
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
                                Utils.Log($"Rename {element.Name} = {formattedString + suffix}");
                            }
                            else
                            {
                                parameter.Set(formattedString);
                                Utils.Log($"Set {parameter.Definition.Name} = {formattedString}");
                            }
                        }
                        else
                        {
                            ret.Add(new RuleFailure
                            {
                                Rule = rule,
                                ElementId = id
                            });
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
                        var exp = parameterValueAsString + expressionString;
                        var context = new ExpressionContext();
                        var e = context.CompileGeneric<bool>(exp);
                        var result = e.Evaluate();
                        if (!result)
                        {
                            ret.Add(new RuleFailure
                            {
                                Rule = rule,
                                ElementId = id,
                                FailureType = FailureType.Regex
                            });
                        }
                    }
                }
                else if (rule.Formula != null)
                {
                    var exp = BuildExpressionString(element, rule.Formula);
                    var context = new ExpressionContext();
                    var e = context.CompileGeneric<double>(exp);
                    var result = e.Evaluate();
                    parametersToSet.Add(new ParameterString(parameter, result.ToString()));
                }
                else if (
                    rule.Regex != null &&
                    parameterValueAsString != null)
                {
                    if (parameterValueAsString == null ||
                        !Regex.IsMatch(parameterValueAsString, rule.Regex))
                    {
                        ret.Add(new RuleFailure
                        {
                            Rule = rule,
                            ElementId = id,
                            FailureType = FailureType.Regex
                        });
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
                        others.Select(q => GetParamAsString(Utils.GetParameter(q, rule.ParameterName))).ToList();
                    if (othersParams.Contains(parameterValueAsString))
                    {
                        using (var form = new FormEnterValue(rule.UserMessage, null))
                        {
                            form.ShowDialog();
                            var v = form.GetValue();
                            parametersToSet.Add(new ParameterString(parameter, v));
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
                            var value = GetParamAsValueString(Utils.GetParameter(host, rule.FromHostInstance));
                            if ((value ?? string.Empty) != (parameterValueAsString ?? string.Empty))
                            {
                                parametersToSet.Add(new ParameterString(parameter, value));
                                TaskDialog.Show("ParameterRule", $"{rule.UserMessage}");
                            }
                        }
                    }
                    else if (element is HostObject host)
                    {
                        var value = GetParamAsValueString(Utils.GetParameter(host, rule.FromHostInstance));
                        var inserts = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilyInstance))
                            .Cast<FamilyInstance>()
                            .Where(q => q.Host != null && q.Host.Id == host.Id);
                        foreach (var insert in inserts)
                        {
                            parametersToSet.Add(new ParameterString(Utils.GetParameter(insert, rule.FromHostInstance), value));
                        }
                    }
                }
                else
                {
                    Utils.Log($"Rule Not Implmented {rule.RuleName}");
                }
            }
            return ret;
        }

        public static string GetParamAsString(Parameter p)
        {
            if (p == null)
                return null;

            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.AsString();
                case StorageType.Double:
                    return p.AsDouble().ToString();
                case StorageType.Integer:
                    return p.AsInteger().ToString();
                case StorageType.ElementId:
                    return p.AsValueString();
            }

            return null;
        }

        public static string BuildExpressionString(Element element, string input)
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

        private static string GetStringAfterParsedParameterName(string input, int matchEnd, int nextMatchIndex)
        {
            var length = nextMatchIndex - matchEnd;
            return input.Substring(matchEnd, length);
        }

        private static string BuildFormattedString(Element element, string input, bool removeIllegalCharacters)
        {
            var matches = Regex.Matches(input, PARAMETER_PARSE_PATTERN);

            var s = string.Empty;
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var matchValueCleaned = match.Value.Replace(PARAMETER_PARSE_START, string.Empty).Replace(PARAMETER_PARSE_END, string.Empty);
                var matchEnd = match.Index + match.Length;
                if (s?.Length == 0)
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

        public static Parameter GetParameterFromElementOrHostOrType(Element e, string paramName)
        {
            var p = Utils.GetParameter(e, paramName);
            if (p != null)
                return p;
            var elementType = e.Document.GetElement(e.GetTypeId());
            if (elementType == null)
                return null;
            p = Utils.GetParameter(elementType, paramName);
            if (p != null)
                return p;
            if (e is FamilyInstance fi)
            {
                p = Utils.GetParameter(fi.Host, paramName);
                if (p != null)
                    return p;
                var hostType = e.Document.GetElement(fi.Host.GetTypeId());
                p = Utils.GetParameter(hostType, paramName);
                if (p != null)
                    return p;
            }
            return null;
        }

        public static string GetParamAsValueString(Parameter p)
        {
            if (p == null)
                return null;
            return p.AsValueString();
        }

        private static string RemoveIllegalCharacters(string s)
        {
            char[] illegal = { '\\', ':', '{', '}', '[', ']', '|', '>', '<', '~', '?', '`', ';', };
            return string.Concat(s.Split(illegal));
        }

        public static List<ParameterRule> GetApplicableParameterRules()
        {
            var applicableParameterRules = Utils.allParameterRules.Where(rule => rule.RevitFileNames == null ||
                       rule.RevitFileNames.FirstOrDefault() == Utils.ALL ||
                       rule.RevitFileNames.Contains(doc.PathName)).ToList();
            return applicableParameterRules;
        }

        public static Parameter GetParameter(Element e, string name)
        {
            var parameters = e.Parameters.Cast<Parameter>().Where(q => q.Definition.Name == name);
            if (parameters.Count() == 0)
            {
                return null;
            }
            else
            {
                if (parameters.Count() > 1)
                {
                    Utils.Log($"Element {GetElementInfo(e)} has multiple parameters named '{name}'", LogLevel.Error);
                }
                return parameters.First();
            }
        }

        public static double GetParamAsDouble(Parameter p)
        {
            if (p.StorageType == StorageType.Integer)
                return Convert.ToDouble(p.AsInteger());
            if (p.StorageType == StorageType.Double)
                return p.AsDouble();
            return double.NaN;
        }

        public static string GetElementInfo(Element e)
        {
            var ret = e.Id.IntegerValue.ToString();
            if (e.Category != null)
            {
                ret += " " + e.Category.Name;
            }
            if (e is FamilyInstance fi)
            {
                ret += " " + fi.Symbol.Family.Name;
            }
            ret += " " + e.Name;
            return ret;
        }

        public enum LogLevel
        {
            Info,
            Error,
            Exception
        }

        public static void LogException(string s, Exception ex)
        {
            Log($"Exception in {s}: {ex.Message} {ex.StackTrace}", LogLevel.Exception);
        }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (level == LogLevel.Error || level == LogLevel.Exception)
            {
                errors.Add(message);
            }
            app.WriteJournalComment($"{PRODUCT_NAME} {level} {message}", true);
        }

        public static List<BuiltInCategory> GetBuiltInCats(Rule rule)
        {
            if (rule.Categories.Count() == 1 && rule.Categories.First() == Utils.ALL)
            {
                return catMap.Values.ToList();
            }
            else
            {
                var builtInCats = rule.Categories.Select(q => catMap[q]).ToList();
                if (rule is ParameterRule parameterRule &&
                    parameterRule.FromHostInstance != null)
                {
                    var hostCats = new List<BuiltInCategory>();
                    foreach (var bic in builtInCats)
                    {
                        if (CatToHostCatMap.ContainsKey(bic))
                        {
                            hostCats.AddRange(CatToHostCatMap[bic]);
                        }
                    }
                    builtInCats.AddRange(hostCats);
                }
                return builtInCats;
            }
        }
    }
}