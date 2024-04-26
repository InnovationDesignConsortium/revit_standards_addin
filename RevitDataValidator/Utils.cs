using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

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

        private static readonly Dictionary<BuiltInCategory, List<BuiltInCategory>> CatToHostCatMap = new Dictionary<BuiltInCategory, List<BuiltInCategory>>()
    {
        { BuiltInCategory.OST_Doors, new List<BuiltInCategory> {BuiltInCategory.OST_Walls } },
        { BuiltInCategory.OST_Windows, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_Roofs } },
        { BuiltInCategory.OST_Rooms, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_RoomSeparationLines } },
    };

        public static Dictionary<string, BuiltInCategory> catMap = new Dictionary<string, BuiltInCategory>();

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