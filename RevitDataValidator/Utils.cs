using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Flee.PublicTypes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using NLog;
using NLog.Config;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public static string userName;
        private const string SCHEMA_NAME = "RevitDataValidator";
        private const string SCHEMA_GUID_STRING = "0B968BB1-3BC4-4458-B4BB-1452AD418F43";
        private const string FIELD_EXCEPTION = "Exception";
        private const string FIELD_RULENAME = "RuleName";
        private const string FIELD_PARAMETERNAME = "ParameterName";

        private static readonly Dictionary<BuiltInCategory, List<BuiltInCategory>> CatToHostCatMap = new Dictionary<BuiltInCategory, List<BuiltInCategory>>()
    {
        { BuiltInCategory.OST_Doors, new List<BuiltInCategory> {BuiltInCategory.OST_Walls } },
        { BuiltInCategory.OST_Windows, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_Roofs } },
        { BuiltInCategory.OST_Rooms, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_RoomSeparationLines } },
    };

        public static Dictionary<string, BuiltInCategory> catMap = new Dictionary<string, BuiltInCategory>();

        public static Result SetExceptionData(Element e, string ruleName, string parameterName, string exceptionMessage)
        {
            if (e == null)
                return Result.Failed;

            Document doc = e.Document;

            Schema mySchema = Schema.ListSchemas().FirstOrDefault(q => q.SchemaName == SCHEMA_NAME);

            if (mySchema == null)
            {
                var guid = Guid.Parse(SCHEMA_GUID_STRING);
                SchemaBuilder sb = new SchemaBuilder(guid);
                sb.SetSchemaName(SCHEMA_NAME);
                sb.AddSimpleField(FIELD_EXCEPTION, typeof(string));
                sb.AddSimpleField(FIELD_RULENAME, typeof(string));
                sb.AddSimpleField(FIELD_PARAMETERNAME, typeof(string));
                mySchema = sb.Finish();
            }

            Entity myEntity = new Entity(mySchema);
            myEntity.Set<string>(mySchema.GetField(FIELD_EXCEPTION), exceptionMessage);
            myEntity.Set<string>(mySchema.GetField(FIELD_RULENAME), ruleName);
            myEntity.Set<string>(mySchema.GetField(FIELD_PARAMETERNAME), ruleName);

            using (Transaction t = new Transaction(doc, "Store Data"))
            {
                bool started = false;
                try
                {
                    t.Start();
                    started = true;
                }
                catch (Exception ex)
                {
                    string message = ex.Message;
                }

                e.SetEntity(myEntity);

                if (started)
                {
                    t.Commit();
                }
            }

            return Result.Succeeded;
        }

        public static bool ElementHasExceptionForRule(Element e, string ruleName, string parameterName, out string exception)
        {
            Schema schema = schema = Schema.ListSchemas().FirstOrDefault(q => q.SchemaName == SCHEMA_NAME);
            exception = "";
            if (schema == null)
            {
                return false;
            }

            Entity fiEntity = e.GetEntity(schema);
            try
            {
                var ruleFromElement = fiEntity.Get<string>(schema.GetField(FIELD_RULENAME));
                var parameterFromElement = fiEntity.Get<string>(schema.GetField(FIELD_PARAMETERNAME));
                exception = fiEntity.Get<string>(schema.GetField(FIELD_EXCEPTION));
                if (ruleFromElement == ruleName &&
                    parameterFromElement == parameterName)
                {
                    return true;
                }
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                return false;
            }

            return false;
        }

        public static void RunWorksetRule(WorksetRule rule, List<ElementId> ids)
        {
            if (rule.RevitFileNames != null &&
                rule.RevitFileNames.FirstOrDefault() != ALL &&
                rule.RevitFileNames.Contains(doc.PathName))
            {
                return;
            }

            var workset = new FilteredWorksetCollector(Utils.doc).FirstOrDefault(q => q.Name == rule.Workset);
            if (workset == null)
            {
                Log($"Workset does not exist {rule.Workset} so will not evaluate rule {rule}", LogLevel.Warn);
                return;
            }

            foreach (ElementId id in ids)
            {
                var element = doc.GetElement(id);

                if (element is ElementType ||
                    element.Category == null ||
                    rule.Categories == null ||
                    (rule.Categories[0] != ALL &&
                    !GetBuiltInCats(rule).Select(q => (int)q).Contains(element.Category.Id.IntegerValue)))
                {
                    continue;
                }

                bool pass = true;
                foreach (var p in rule.Parameters)
                {
                    var parameter = GetParameterFromElementOrHostOrType(element, p.Name);
                    if (parameter == null)
                    {
                        pass = false;
                        break;
                    }

                    var paramValue = GetParamAsString(parameter);
                    if (paramValue != p.Value)
                    {
                        pass = false;
                        break;
                    }
                }

                if (pass)
                {
                    var parameter = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (parameter.IsReadOnly)
                    {
                        Log($"Workset parameter is readonly for {GetElementInfo(element)}", LogLevel.Warn);
                    }
                    else
                    {
                        try
                        {
                            parameter.Set(workset.Id.IntegerValue);
                            Log($"Set workset of {GetElementInfo(element)} to {workset.Name}", LogLevel.Info);
                        }
                        catch (Exception ex)
                        {
                            LogException($"Exception setting workset for {GetElementInfo(element)}", ex);
                        }
                    }
                }
            }
        }

        public static List<ElementId> RunCustomRule(ParameterRule rule)
        {
            Type type = dictCustomCode[rule.CustomCode];
            object obj = Activator.CreateInstance(type);
            object x = type.InvokeMember("Run",
                                BindingFlags.Default | BindingFlags.InvokeMethod,
                                null,
                                obj,
                                new object[] { doc });
            if (x is List<ElementId> ids)
            {
                return ids;
            }
            return new List<ElementId>();
        }

        public static RuleFailure RunParameterRule(
            ParameterRule rule,
            ElementId id,
            List<ParameterString> inputParameterValues,
            out List<ParameterString> parametersToSet,
            out List<ParameterString> parametersToSetForFormatRules
            )
        {
            parametersToSetForFormatRules = new List<ParameterString>();
            parametersToSet = new List<ParameterString>();
            var element = doc.GetElement(id);

            if (element.Category == null ||
                (rule.Categories == null && rule.ElementClasses == null) ||
                (rule.ElementClasses?.Any(q => q.EndsWith(element.GetType().Name)) == false) ||
                (rule.Categories != null && rule.Categories.FirstOrDefault() != ALL &&
                !GetBuiltInCats(rule).Select(q => (int)q).Contains(element.Category.Id.IntegerValue)))
            {
                return null;
            }

            var parameter = GetParameter(element, rule.ParameterName);
            if (parameter == null)
                return null;

            var parameterValueAsString = GetParamAsString(parameter);
            if (inputParameterValues?.FirstOrDefault(q => q.Parameter.Definition.Name == rule.ParameterName) != null)
            {
                var parameterStringMatch = inputParameterValues.Find(q => q.Parameter.Definition.Name == rule.ParameterName);
                parameter = parameterStringMatch.Parameter;
                parameterValueAsString = parameterStringMatch.NewValue;
            }
            if (parameterValueAsString == null)
            {
                return null;
            }

            if (ElementHasExceptionForRule(element, rule.RuleName, rule.ParameterName, out string execption))
            {
                Log($"{rule.RuleName}|'{GetElementInfo(element)}'|Not running rule for parameter '{parameter.Definition.Name}' due to exception {execption}", LogLevel.Trace);
            }

            Log($"{rule.RuleName}|'{GetElementInfo(element)}'|Running rule for parameter '{parameter.Definition.Name}'", LogLevel.Trace);

            if (rule.KeyValues != null ||
                rule.ListOptions != null)
            {
                if (rule.ListOptions != null && (parameterValueAsString == null ||
                    !rule.ListOptions.Select(q => q.Name).Contains(parameterValueAsString)))
                {
                    Log($"{rule.RuleName}|{GetElementInfo(element)}|'{parameter.Definition.Name}' value '{parameterValueAsString}' is not a valid value. Valid values are [{string.Join(", ", rule.ListOptions)}]", LogLevel.Warn);
                    return new RuleFailure
                    {
                        Rule = rule,
                        ElementId = id,
                        FailureType = FailureType.List
                    };
                }
                else if (rule.KeyValues != null)
                {
                    var keys = rule.KeyValues.Find(q => q[0] == parameterValueAsString);
                    if (keys == null)
                    {
                        Log($"{rule.RuleName}|{GetElementInfo(element)}|{parameterValueAsString} is not a valid key value. Valid values are [{string.Join(", ", rule.KeyValues)}]", LogLevel.Warn);
                        return new RuleFailure
                        {
                            Rule = rule,
                            ElementId = id,
                            FailureType = FailureType.List
                        };
                    }
                    for (var i = 0; i < rule.DrivenParameters.Count; i++)
                    {
                        var drivenParam = GetParameter(element, rule.DrivenParameters[i]);
                        if (drivenParam == null)
                        {
                            Log($"{rule.RuleName}|{GetElementInfo(element)}|Cannot set the driven parameter {rule.DrivenParameters[i]} which does not exist", LogLevel.Warn);
                            continue;
                        }
                        parametersToSet.Add(new ParameterString(drivenParam, keys[i + 1]));
                    }
                }
            }
            else if (rule.Format != null)
            {
                var formattedString = BuildFormattedString(element, rule.Format, true);
                if (formattedString != null &&
                    !parameterValueAsString.StartsWith(formattedString))
                {
                    if (parameter.Definition.Name == "Type Name")
                    {
                        Type t = element.GetType();
                        var i = 0;
                        var suffix = string.Empty;

                        while (new FilteredElementCollector(doc)
                            .OfClass(t).Any(q => q.Name == formattedString + suffix))
                        {
                            i++;
                            suffix = " " + i.ToString();
                        }
                        var formattedWithSuffix = formattedString + suffix;
                        parametersToSetForFormatRules.Add(new ParameterString(parameter, formattedWithSuffix, parameterValueAsString));
                        Log($"Renaming type '{element.Name}' to '{formattedString + suffix}' to match format '{rule.Format}'", LogLevel.Info);
                    }
                    else
                    {
                        Log($"Renaming '{GetElementInfo(element)}' '{parameter.Definition.Name}' to '{formattedString}' to match format '{rule.Format}'", LogLevel.Info);
                        parametersToSet.Add(new ParameterString(parameter, formattedString));
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
                    Log($"Evaluating IF {ifClause} THEN {thenClause}", LogLevel.Trace);
                    var ifExp = BuildExpressionString(element, ifClause, inputParameterValues);
                    var ifExpIsTrue = CSharpScript.EvaluateAsync<bool>(ifExp,
                         Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                         .WithImports("System")
                         ).Result;

                    if (ifExpIsTrue)
                    {
                        Log("IF clause is True: " + ifExp, LogLevel.Trace);
                        var thenExp = BuildExpressionString(element, thenClause, inputParameterValues);
                        var thenExpIsTrue = CSharpScript.EvaluateAsync<bool>(thenExp,
                         Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                         .WithImports("System")
                         ).Result;
                        if (thenExpIsTrue)
                        {
                            Log("THEN clause is True: " + thenExp, LogLevel.Trace);
                        }
                        else
                        {
                            Log($"{rule.RuleName}|{GetElementInfo(element)}|THEN clause '{thenClause}' is False: {thenExp}", LogLevel.Warn);
                            return new RuleFailure
                            {
                                Rule = rule,
                                ElementId = id,
                                FailureType = FailureType.IfThen
                            };
                        }
                    }
                    else
                    {
                        Log("IF clause is False: " + ifExp, LogLevel.Trace);
                    }
                }
                else
                {
                    var expressionString = BuildExpressionString(element, rule.Requirement);
                    string exp = parameterValueAsString + " " + expressionString;
                    var context = new ExpressionContext();
                    var e = context.CompileGeneric<bool>(exp);
                    var result = e.Evaluate();
                    if (result)
                    {
                        Log($"Evaluated '{exp}' for '{rule.ParameterName} {rule.Requirement}'. Rule passed", LogLevel.Trace);
                    }
                    else
                    {
                        Log($"{rule.RuleName}|{GetElementInfo(element)}|Evaluated '{exp}' for '{rule.ParameterName} {rule.Requirement}'. Rule failed!", LogLevel.Warn);
                        return new RuleFailure
                        {
                            Rule = rule,
                            ElementId = id,
                            FailureType = FailureType.Regex
                        };
                    }
                }
            }
            else if (rule.Formula != null)
            {
                var exp = BuildExpressionString(element, rule.Formula);
                var context = new ExpressionContext();
                var e = context.CompileGeneric<double>(exp);
                var result = e.Evaluate();
                Log($"Setting {parameter.Definition.Name} to {result} to match formula {rule.Formula}", LogLevel.Info);
                parametersToSet.Add(new ParameterString(parameter, result.ToString()));
            }
            else if (
                rule.Regex != null &&
                parameterValueAsString != null)
            {
                if (parameterValueAsString == null ||
                    !Regex.IsMatch(parameterValueAsString, rule.Regex))
                {
                    Log($"{rule.RuleName}|{GetElementInfo(element)}|'{rule.ParameterName}' value '{parameterValueAsString}' does not match regex {rule.Regex}", LogLevel.Warn);
                    return new RuleFailure
                    {
                        Rule = rule,
                        ElementId = id,
                        FailureType = FailureType.Regex
                    };
                }
                else
                {
                    Log($"{rule.ParameterName} value {parameterValueAsString} matches regex {rule.Regex}", LogLevel.Trace);
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
                    Log($"{rule.RuleName}|{GetElementInfo(element)}|Found duplicates of {parameterValueAsString} for {rule.ParameterName}", LogLevel.Warn);
                    return new RuleFailure
                    {
                        Rule = rule,
                        ElementId = id,
                        FailureType = FailureType.PreventDuplicates
                    };
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
                        Log($"Using value '{value}' from insert {GetElementInfo(fi)} to set value of {parameter.Definition.Name} for host {GetElementInfo(host)}", LogLevel.Info);
                    }
                }
                else if (element is HostObject host)
                {
                    var value = GetParamAsValueString(Utils.GetParameter(host, rule.FromHostInstance));
                    var inserts = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(q => q.Host != null && q.Host.Id == host.Id).ToList();
                    Log($"Using value '{value}' from host {GetElementInfo(host)} to set values for {rule.FromHostInstance} for inserts {string.Join(", ", inserts.Select(q => GetElementInfo(q)))}", LogLevel.Info);
                    foreach (var insert in inserts)
                    {
                        parametersToSet.Add(new ParameterString(Utils.GetParameter(insert, rule.FromHostInstance), value));
                    }
                }
            }
            else
            {
                Log($"Rule Not Implmented {rule.RuleName}", LogLevel.Error);
            }
            return null;
        }

        public static TaskDialog GetTaskDialogForFormatRenaming(ParameterRule rule, List<ParameterString> thisRuleParametersToSetForFormatRules)
        {
            return new TaskDialog("Alert")
            {
                MainInstruction =
        $"{rule.ParameterName} does not match the required format {rule.Format} and will be renamed",
                MainContent = string.Join(Environment.NewLine, thisRuleParametersToSetForFormatRules.Select(q => $"From '{q.OldValue}' to '{q.NewValue}'")),
                CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel
            };
        }

        public static List<RuleFailure> GetFailures(ElementId id, List<ParameterString> inputParameterValues, out List<ParameterString> parametersToSet)
        {
            var ret = new List<RuleFailure>();
            parametersToSet = new List<ParameterString>();
            foreach (var rule in GetApplicableParameterRules())
            {
                var ruleFailure = RunParameterRule(
                    rule,
                    id,
                    inputParameterValues,
                    out List<ParameterString> thisRuleParametersToSet,
                    out List<ParameterString> thisRuleParametersToSetForFormatRules
                    );
                parametersToSet.AddRange(thisRuleParametersToSet);

                if (thisRuleParametersToSetForFormatRules.Any())
                {
                    var td = GetTaskDialogForFormatRenaming(rule, thisRuleParametersToSetForFormatRules);
                    if (td.Show() == TaskDialogResult.Ok)
                    {
                        parametersToSet.AddRange(thisRuleParametersToSetForFormatRules);
                    }
                    else
                    {
                        ret.Add(new RuleFailure { Rule = rule, ElementId = id });
                    }
                }

                if (ruleFailure != null)
                    ret.Add(ruleFailure);
            }
            return ret;
        }

        public static string GetParamAsString(Parameter p)
        {
            if (p == null)
                return null;

            if (p.StorageType == StorageType.String)
            {
                return p.AsString();
            }
            else if (p.StorageType == StorageType.Integer)
            {
                return p.AsInteger().ToString();
            }
            else if (p.StorageType == StorageType.Double)
            {
                var paramAsDouble = GetParamAsDouble(p);
                double paramValue;
                try
                {
                    var unitTypeId = p.GetUnitTypeId();
                    paramValue = UnitUtils.ConvertFromInternalUnits(paramAsDouble, unitTypeId);
                }
                catch
                {
                    paramValue = paramAsDouble;
                }
                return paramValue.ToString();
            }
            else
            {
                return p.AsValueString();
            }
        }

        public static string BuildExpressionString(Element element, string input, List<ParameterString> inputParameterValues = null)
        {
            var matches = Regex.Matches(input, PARAMETER_PARSE_PATTERN);
            if (matches.Count == 0)
            {
                return input;
            }

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
                    string parameterNewValueAsString = null;
                    if (inputParameterValues?.FirstOrDefault(q => q.Parameter.Definition.Name == parameter.Definition.Name) != null)
                    {
                        var parameterStringMatch = inputParameterValues.Find(q => q.Parameter.Definition.Name == parameter.Definition.Name);
                        parameter = parameterStringMatch.Parameter;
                        parameterNewValueAsString = parameterStringMatch.NewValue;
                    }

                    if (parameter.StorageType == StorageType.Integer || parameter.StorageType == StorageType.Double)
                    {
                        if (parameterNewValueAsString != null)
                        {
                            if (int.TryParse(parameterNewValueAsString, out int iValue))
                            {
                                s += iValue;
                            }
                            else if (double.TryParse(parameterNewValueAsString, out double dValue))
                            {
                                s += dValue;
                            }
                        }
                        else
                        {
                            var paramAsDouble = GetParamAsDouble(parameter);
                            double paramValue;
                            try
                            {
                                var unitTypeId = parameter.GetUnitTypeId();
                                paramValue = UnitUtils.ConvertFromInternalUnits(paramAsDouble, unitTypeId);
                            }
                            catch
                            {
                                paramValue = paramAsDouble;
                            }
                            s += paramValue;
                        }
                    }
                    else if (parameter.StorageType == StorageType.String)
                    {
                        if (parameterNewValueAsString != null)
                        {
                            s += "\"" + parameterNewValueAsString + "\"";
                        }
                        else
                        {
                            s += "\"" + parameter.AsString() + "\"";
                        }
                    }
                    else if (parameter.StorageType == StorageType.ElementId)
                    {
                        if (parameterNewValueAsString != null)
                        {
                            s += "\"" + parameterNewValueAsString + "\"";
                        }
                        else
                        {
                            s += "\"" + parameter.AsValueString() + "\"";
                        }
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
                if (parameter == null)
                {
                    Log($"BuildFormattedString parameter {matchValueCleaned} does not exist for element {GetElementInfo(element)}", LogLevel.Info);
                    return null;
                }

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
                if (fi.Host != null)
                {
                    var hostType = e.Document.GetElement(fi.Host.GetTypeId());
                    p = Utils.GetParameter(hostType, paramName);
                    if (p != null)
                        return p;
                }
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
            return Utils.allParameterRules.Where(rule => rule.RevitFileNames == null ||
                       rule.RevitFileNames.FirstOrDefault() == Utils.ALL ||
                       rule.RevitFileNames.Contains(doc.PathName)).ToList();
        }

        public static Parameter GetParameter(Element e, string name)
        {
            if (e == null) return null;

            var parameters = e.Parameters.Cast<Parameter>().Where(q => q?.Definition?.Name == name);
            if (!parameters.Any())
            {
                return null;
            }
            else
            {
                var internalDuplicates = new List<string> { "Level", "Design Option", "View Template" };
                if ((parameters.Count() > 1 && !internalDuplicates.Contains(parameters.First().Definition.Name)) ||
                    (parameters.Count() > 2 && internalDuplicates.Contains(parameters.First().Definition.Name)))
                {
                    Utils.Log($"{GetElementInfo(e)} has multiple '{name}' parameters", LogLevel.Warn);
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
            var ret = "";
            if (e.Category != null)
            {
                ret += e.Category.Name + ":";
            }
            if (e is FamilyInstance fi)
            {
                ret += fi.Symbol.Family.Name + ":";
            }
            ret += $"{e.Name}:{e.Id.IntegerValue}";
            return ret;
        }

        public enum LogLevel
        {
            Warn,
            Info,
            Error,
            Exception,
            Trace
        }

        public static void LogException(string s, Exception ex)
        {
            Log($"Exception in {s}: {ex.Message} {ex.StackTrace}", LogLevel.Exception);
        }

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static string GetFileName()
        {
            if (doc == null)
                return "";

            if (doc.IsWorkshared)
                return ModelPathUtils.ConvertModelPathToUserVisiblePath(doc.GetWorksharingCentralModelPath());

            return doc.PathName;
        }

        public static void Log(string message, LogLevel level)
        {
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(dllPath, "NLog.config"));
            message = Path.GetFileName(GetFileName()) + "|" + message;
            if (level == LogLevel.Info)
            {
                Logger.Info(message);
            }
            else if (level == LogLevel.Error)
            {
                Logger.Error(message);
            }
            else if (level == LogLevel.Warn)
            {
                Logger.Warn(message);
            }
            else if (level == LogLevel.Exception)
            {
                Logger.Error(message);
            }
            else if (level == LogLevel.Trace)
            {
                Logger.Trace(message);
            }
            else
            {
                Logger.Error(message);
            }
        }

        public static List<BuiltInCategory> GetBuiltInCats(Rule rule)
        {
            if (rule.Categories.Count == 1 && rule.Categories[0] == Utils.ALL)
            {
                return catMap.Values.ToList();
            }
            else
            {
                var builtInCats = rule.Categories.ConvertAll(q => catMap[q]);
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