using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitDataValidator
{
    public static class Utils
    {
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

        public static void LogException(string s, Exception ex)
        {
            LogError($"Exception in {s}: {ex.Message}");
            LogError(ex.StackTrace);
        }
        public static void Log(string message)
        {
            Utils.app.WriteJournalComment(PRODUCT_NAME + " " + message, true);
        }

        public static void LogError(string error)
        {
            Utils.errors.Add(error);
            Utils.app.WriteJournalComment(PRODUCT_NAME + " " + error, true);
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