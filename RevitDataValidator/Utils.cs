using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Flee.PublicTypes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Config;
using Octokit;
using RevitDataValidator.Classes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;

#if !PRE_NET_8
[assembly: SupportedOSPlatform("windows")]
#endif

namespace RevitDataValidator
{
    public static class Utils
    {
        public static string dialogIdShowing = "";
        public static ControlledApplication app;
        public static string PRODUCT_NAME = "Revit Standards Addin";
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
        public const string panelName = "Data Validator";
        private const string TAB_NAME = "Add-Ins";
        public static Dictionary<string, string> dictFileActivePackSet = new Dictionary<string, string>();
        public static Dictionary<string, RuleFileInfo> ruleDatas = new Dictionary<string, RuleFileInfo>();
        public static Dictionary<string, RuleFileInfo> parameterPackDatas = new Dictionary<string, RuleFileInfo>();
        public static string MsiToRunOnExit = null;
        public static string GIT_OWNER = "";
        public static string GIT_REPO = "";

        private static readonly Dictionary<BuiltInCategory, List<BuiltInCategory>> CatToHostCatMap = new Dictionary<BuiltInCategory, List<BuiltInCategory>>()
    {
        { BuiltInCategory.OST_Doors, new List<BuiltInCategory> {BuiltInCategory.OST_Walls } },
        { BuiltInCategory.OST_Windows, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_Roofs } },
        { BuiltInCategory.OST_Rooms, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_RoomSeparationLines } },
    };

        public static Dictionary<string, BuiltInCategory> catMap = new Dictionary<string, BuiltInCategory>();
        public const string GIT_CODE_REPO_OWNER = "InnovationDesignConsortium";
        public const string GIT_CODE_REPO_NAME = "revit_standards_addin";

        public static void DownloadAsset(string tag, Asset asset)
        {
            try
            {
                var fileName = Path.Combine(dllPath, asset.name);
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                var githubToken = GetGithubTokenFromApp();
                // https://github.com/gruntwork-io/fetch
                var arguments = $"-repo https://github.com/{GIT_CODE_REPO_OWNER}/{GIT_CODE_REPO_NAME} --tag=\"{tag}\" --release-asset=\"{asset.name}\" --github-oauth-token {githubToken} {dllPath}";

                StartShell(
                    $"{dllPath}\\fetch_windows_amd64.exe", false, arguments);

                MsiToRunOnExit = fileName;
            }
            catch (Exception ex)
            {
                LogException("Exception downloading update:", ex);
            }
        }

        public static bool IsWebVersionNewer(Version webVersion)
        {
            return webVersion.CompareTo(GetInstalledVersion()) > 0;
        }

        public static GithubResponse GetLatestWebRelase()
        {
            var url = $"https://api.github.com/repos/{GIT_CODE_REPO_OWNER}/{GIT_CODE_REPO_NAME}/releases";

            var githubToken = GetGithubTokenFromApp();
            var releasesJson = GetPrivateRepoString(url, HttpMethod.Get, githubToken, "application/vnd.github.v3.raw", "token");

            if (releasesJson == null)
            {
                return null;
            }
            var releases = JsonConvert.DeserializeObject<List<GithubResponse>>(releasesJson);
            if (releases == null)
            {
                return null;
            }
            var latestRelease = releases
                .Where(q => !q.draft)
                .Where(q => !q.prerelease)
                    .OrderByDescending(release => release.published_at)
                    .FirstOrDefault();
            if (latestRelease == null || latestRelease.assets.Count == 0)
            {
                return null;
            }
            else
            {
                return latestRelease;
            }
        }

        private static string GetGithubTokenFromApp()
        {
            // https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/authenticating-as-a-github-app-installation

            // 1 - Generate a JSON web token (JWT) for your app

            var tokenForApp = GenerateJwtToken();
            if (string.IsNullOrEmpty(tokenForApp))
            {
                Log("JwtToken is empty", LogLevel.Error);
                return null;
            }

            // 2 - Get the ID of the installation that you want to authenticate as
            var installationResponse = GetPrivateRepoString("https://api.github.com/app/installations", HttpMethod.Get, tokenForApp, "application/vnd.github+json", "Bearer");
            var installations = ((JArray)JsonConvert.DeserializeObject(installationResponse)).ToObject<List<GitHubAppInstallation>>();
            var installation = installations?.FirstOrDefault(q => q.account.login == GIT_OWNER);
            if (installation == null)
            {
                var td = new TaskDialog("Error")
                {
                    MainInstruction = $"Github app must be installed for {GIT_OWNER}",
                    MainContent = "\"<a href=\"https://github.com/apps/revitstandardsgithubapp/installations/new\">https://github.com/apps/revitstandardsgithubapp/installations/new</a>\""
                };
                td.Show();

                Log($"Installation does not exist for {GIT_OWNER}", LogLevel.Error);
                return null;
            }
            var instalationId = installation?.id;

            // 3 - Send a REST API POST request to /app/installations/INSTALLATION_ID/access_tokens
            var myJsonResponse3 = GetPrivateRepoString($"https://api.github.com/app/installations/{instalationId}/access_tokens", HttpMethod.Post, tokenForApp, "application/vnd.github+json", "Bearer");
            var amazing = JsonConvert.DeserializeObject<RootB>(myJsonResponse3);
            var tokenNoWay = amazing?.token;
            return tokenNoWay;
        }

        public static RepositoryContent GetGitData(ContentType contentType, string path)
        {
            try
            {
                var tokenFromGithubApp = GetGithubTokenFromApp();

                var client = new GitHubClient(new Octokit.ProductHeaderValue("revit-datavalidator"))
                {
                    Credentials = new Credentials(tokenFromGithubApp)
                };

                var content = client.Repository.Content.GetAllContents(GIT_OWNER, GIT_REPO, path);

                if (content == null || content.IsFaulted)
                {
                    Log($"No git data found at {path}", LogLevel.Warn);
                    return null;
                }

                if (content.Result == null)
                {
                    Log($"No git data found at {path}", LogLevel.Warn);
                    return null;
                }

                var result = content.Result.Where(q => q.Type == contentType);
                if (result == null)
                {
                    Log($"No git data found at {path} for {contentType}", LogLevel.Warn);
                    return null;
                }
                return result.FirstOrDefault();
            }
            catch (Exception ex)
            {
                LogException("GetGitData", ex);
                return null;
            }
        }

        private static string GenerateJwtToken()
        {
            try
            {
                var pathtoexe = Path.Combine(dllPath, "CreateJsonWebToken", "CreateJsonWebToken.exe");
                if (File.Exists(pathtoexe))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = pathtoexe,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    };
                    var pp = Process.Start(startInfo);
                    var output = pp.StandardOutput.ReadToEnd();
                    pp.WaitForExit();
                    return output;
                }
                else
                {
                    return "";
                }
            }
            catch (Exception ex)
            {
                Utils.LogException("Failed to generate JwtToken", ex);
                return null;
            }
        }

        public static Version GetInstalledVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        public static string GetPrivateRepoString(string url, HttpMethod method, string githubToken, string accept, string authenticationHeader)
        {
            Stream stream = null;
            try
            {
#if PRE_NET_8
                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                string methodString;
                if (method == HttpMethod.Get)
                {
                    methodString = "GET";
                }
                else
                {
                    methodString = "POST";
                }
                request.Method = methodString;
                request.UserAgent = "Revit Standards Addin";
                request.Accept = accept;
                request.Headers.Add("Authorization", $"{authenticationHeader} {githubToken}");
                var response = request.GetResponse();
                stream = response.GetResponseStream();
#else
                HttpResponseMessage request;
                using (var requestMessage = new HttpRequestMessage(method, url))
                {
                    requestMessage.Headers.UserAgent.ParseAdd("Revit Standards Addin");
                    requestMessage.Headers.Accept.ParseAdd(accept);
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue(authenticationHeader, githubToken);
                    var httpClient = new HttpClient();
                    request = httpClient.Send(requestMessage);
                }
                stream = request.Content.ReadAsStream();
#endif
            }
            catch (Exception ex)
            {
                LogException("GetPrivateRepoStream", ex);
            }
            if (stream == null)
            {
                return null;
            }
            return new StreamReader(stream).ReadToEnd();
        }

        public static Result SetReasonAllowed(Element e, string ruleName, string parameterName, string exceptionMessage)
        {
            if (e == null)
                return Result.Failed;

            var doc = e.Document;

            var mySchema = Schema.ListSchemas().FirstOrDefault(q => q.SchemaName == SCHEMA_NAME);

            if (mySchema == null)
            {
                var guid = Guid.Parse(SCHEMA_GUID_STRING);
                var sb = new SchemaBuilder(guid);
                sb.SetSchemaName(SCHEMA_NAME);
                sb.AddSimpleField(FIELD_EXCEPTION, typeof(string));
                sb.AddSimpleField(FIELD_RULENAME, typeof(string));
                sb.AddSimpleField(FIELD_PARAMETERNAME, typeof(string));
                mySchema = sb.Finish();
            }

            var myEntity = new Entity(mySchema);
            myEntity.Set<string>(mySchema.GetField(FIELD_EXCEPTION), exceptionMessage);
            myEntity.Set<string>(mySchema.GetField(FIELD_RULENAME), ruleName);
            myEntity.Set<string>(mySchema.GetField(FIELD_PARAMETERNAME), parameterName);

            using (var t = new Transaction(doc, "Store Data"))
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

        public static bool ElementHasReasonAllowedForRule(Element e, string ruleName, string parameterName, out string exception)
        {
            var schema = Schema.ListSchemas().FirstOrDefault(q => q.SchemaName == SCHEMA_NAME);
            exception = "";
            if (schema == null)
            {
                return false;
            }

            var fiEntity = e.GetEntity(schema);
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
            var workset = new FilteredWorksetCollector(doc).FirstOrDefault(q => q.Name == rule.Workset);
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
                    !GetBuiltInCats(rule).Select(q => ElementIdExtension.GetValue(BuiltInCategoryExtension.GetElementId(q))).Contains(ElementIdExtension.GetValue(element.Category.Id))))
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

        public static IEnumerable<ElementId> RunCustomRule(ParameterRule rule)
        {
            Type type = dictCustomCode[rule.CustomCode];
            object obj = Activator.CreateInstance(type);
            object x = type.InvokeMember("Run",
                                BindingFlags.Default | BindingFlags.InvokeMethod,
                                null,
                                obj,
                                new object[] { doc });
            if (x is IEnumerable<ElementId> ids)
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

            if (element == null ||
                element.Category == null ||
                (rule.Categories == null && rule.ElementClasses == null) ||
                (rule.ElementClasses?.Any(q => q.EndsWith(element.GetType().Name)) == false) ||
                (rule.Categories != null && rule.Categories.FirstOrDefault() != ALL &&
                !GetBuiltInCats(rule).Select(q => ElementIdExtension.GetValue(BuiltInCategoryExtension.GetElementId(q))).Contains(ElementIdExtension.GetValue(element.Category.Id))))
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

            // https://github.com/InnovationDesignConsortium/revit_standards_addin/issues/17
            // rule should run if target paramater has no value
            //if (parameterValueAsString == null)
            //{
            //    return null;
            //}

            if (ElementHasReasonAllowedForRule(element, rule.RuleName, rule.ParameterName, out string reasonAllowed))
            {
                Log($"{rule.RuleName}|'{GetElementInfo(element)}'|Not running rule for parameter '{parameter.Definition.Name}'. It is allowed because '{reasonAllowed}'", LogLevel.Trace);
                return null;
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
                if (formattedString != null)
                {
                    if (parameter.Definition.Name == "Type Name")
                    {
                        if (parameterValueAsString?.StartsWith(formattedString) == false)
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
                var bic = (BuiltInCategory)(ElementIdExtension.GetValue(element.Category.Id));
                var others = new FilteredElementCollector(doc)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .Where(q => q.Id != element.Id);
                List<string> othersParams =
                    others.Select(q => GetParamAsString(GetParameter(q, rule.ParameterName))).ToList();
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
                        var value = GetParamAsValueString(GetParameter(host, rule.FromHostInstance));
                        if ((value ?? string.Empty) != (parameterValueAsString ?? string.Empty))
                        {
                            parametersToSet.Add(new ParameterString(parameter, value));
                            Autodesk.Revit.UI.TaskDialog.Show("ParameterRule", $"{rule.UserMessage}");
                        }
                        Log($"Using value '{value}' from insert {GetElementInfo(fi)} to set value of {parameter.Definition.Name} for host {GetElementInfo(host)}", LogLevel.Info);
                    }
                }
                else if (element is HostObject host)
                {
                    var value = GetParamAsValueString(GetParameter(host, rule.FromHostInstance));
                    var inserts = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(q => q.Host != null && q.Host.Id == host.Id).ToList();
                    Log($"Using value '{value}' from host {GetElementInfo(host)} to set values for {rule.FromHostInstance} for inserts {string.Join(", ", inserts.Select(q => GetElementInfo(q)))}", LogLevel.Info);
                    foreach (var insert in inserts)
                    {
                        parametersToSet.Add(new ParameterString(GetParameter(insert, rule.FromHostInstance), value));
                    }
                }
            }
            else
            {
                Log($"Rule Not Implmented {rule.RuleName}", LogLevel.Error);
            }
            return null;
        }

        public static Autodesk.Revit.UI.TaskDialog GetTaskDialogForFormatRenaming(ParameterRule rule, List<ParameterString> thisRuleParametersToSetForFormatRules)
        {
            return new Autodesk.Revit.UI.TaskDialog("Alert")
            {
                MainInstruction =
        $"{rule.ParameterName} does not match the required format {rule.Format} and will be renamed",
                MainContent = string.Join(Environment.NewLine, thisRuleParametersToSetForFormatRules.Select(q => $"From '{q.OldValue}' to '{q.NewValue}'")),
                CommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons.Ok | Autodesk.Revit.UI.TaskDialogCommonButtons.Cancel
            };
        }

        public static List<RuleFailure> GetFailures(ElementId id, List<ParameterString> inputParameterValues, out List<ParameterString> parametersToSet)
        {
            var ret = new List<RuleFailure>();
            parametersToSet = new List<ParameterString>();
            foreach (var rule in allParameterRules)
            {
                var ruleFailure = RunParameterRule(
                    rule,
                    id,
                    inputParameterValues,
                    out List<ParameterString> thisRuleParametersToSet,
                    out List<ParameterString> thisRuleParametersToSetForFormatRules
                    );
                parametersToSet.AddRange(thisRuleParametersToSet);

                if (thisRuleParametersToSetForFormatRules.Count != 0)
                {
                    var td = GetTaskDialogForFormatRenaming(rule, thisRuleParametersToSetForFormatRules);
                    if (td.Show() == Autodesk.Revit.UI.TaskDialogResult.Ok)
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

        private static Parameter GetParameterFromElementOrHostOrType(Element e, string paramName)
        {
            var p = GetParameter(e, paramName);
            if (p != null)
                return p;
            var elementType = e.Document.GetElement(e.GetTypeId());
            if (elementType == null)
                return null;
            p = GetParameter(elementType, paramName);
            if (p != null)
                return p;
            if (e is FamilyInstance fi)
            {
                p = GetParameter(fi.Host, paramName);
                if (p != null)
                    return p;
                if (fi.Host != null)
                {
                    var hostType = e.Document.GetElement(fi.Host.GetTypeId());
                    p = GetParameter(hostType, paramName);
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

        public static Parameter GetParameter(Element e, string name)
        {
            if (e == null) return null;

            var parameters = e.Parameters.Cast<Parameter>().Where(q => q?.Definition?.Name == name);
            if (parameters.Any())
            {
                var internalDuplicates = new List<string> { "Level", "Design Option", "View Template" };
                if ((parameters.Count() > 1 && !internalDuplicates.Contains(parameters.First().Definition.Name)) ||
                    (parameters.Count() > 2 && internalDuplicates.Contains(parameters.First().Definition.Name)))
                {
                    Log($"{GetElementInfo(e)} has multiple '{name}' parameters", LogLevel.Warn);
                }
                return parameters.First();
            }
            else
            {
                return null;
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
            ret += $"{e.Name}:{ElementIdExtension.GetValue(e.Id)}";
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
            if (Environment.GetEnvironmentVariable("RevitDataValidatorDebug", EnvironmentVariableTarget.Machine) == "1")
            {
                var td = new TaskDialog("Error")
                {
                    MainInstruction = ex.Message,
                    MainContent = ex.StackTrace
                };
                td.Show();
            }
            Log($"Exception in {s}: {ex.Message} {ex.StackTrace}", LogLevel.Exception);
        }

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static string GetFileName(Document doc = null)
        {
            if (doc == null)
            {
                doc = Utils.doc;
            }
            if (doc == null)
            {
                return "";
            }

            if (doc.IsWorkshared)
            {
                return ModelPathUtils.ConvertModelPathToUserVisiblePath(doc.GetWorksharingCentralModelPath());
            }
            else
            {
                if (doc.PathName == string.Empty)
                {
                    return string.Empty;
                }
                else
                {
                    return doc.PathName;
                }
            }
        }

        public static Process StartShell(string toolPath, bool useShell, string arguments = "")
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = useShell
            };

            return Process.Start(startInfo);
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
                Autodesk.Revit.UI.TaskDialog.Show("Error", message);
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
            if (rule.Categories.Count == 1 && rule.Categories[0] == ALL)
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
                        if (CatToHostCatMap.TryGetValue(bic, out List<BuiltInCategory> value))
                        {
                            hostCats.AddRange(value);
                        }
                    }
                    builtInCats.AddRange(hostCats);
                }
                return builtInCats;
            }
        }
    }
}