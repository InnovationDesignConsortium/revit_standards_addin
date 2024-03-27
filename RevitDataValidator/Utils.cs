using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;

namespace RevitDataValidator
{
    public static class Utils
    {
        public static ControlledApplication app;
        public static string PRODUCT_NAME = "RevitValidator";
        public static readonly string ALL = "<all>";
        public static readonly char LIST_SEP = ',';
        public static List<Rule> allRules;
        public static List<string> errors;
        public static PropertiesPanel propertiesPanel;
        public static DockablePaneId paneId;
        public static ParameterUIData parameterUIData;
        public static ISet<ElementId> selectedIds;
        public static Document doc;
        public static EventHandlerWithProperty eventHandlerWithProperty;

        private static readonly Dictionary<BuiltInCategory, List<BuiltInCategory>> CatToHostCatMap = new Dictionary<BuiltInCategory, List<BuiltInCategory>>()
    {
        { BuiltInCategory.OST_Doors, new List<BuiltInCategory> {BuiltInCategory.OST_Walls } },
        { BuiltInCategory.OST_Windows, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_Roofs } },
        { BuiltInCategory.OST_Rooms, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_RoomSeparationLines } },
    };

        public static Dictionary<string, BuiltInCategory> catMap = new Dictionary<string, BuiltInCategory>();

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
                //if (rule.RuleType == RuleType.FromHostInstance ||
                //    rule.RuleType == RuleType.FromHostType ||
                //    rule.RuleType == RuleType.Calculated)
                //{
                //    var hostCats = new List<BuiltInCategory>();
                //    foreach (var bic in builtInCats)
                //    {
                //        if (CatToHostCatMap.ContainsKey(bic))
                //        {
                //            hostCats.AddRange(CatToHostCatMap[bic]);
                //        }
                //    }
                //    builtInCats.AddRange(hostCats);
                //}
                return builtInCats;
            }
        }
    }
}