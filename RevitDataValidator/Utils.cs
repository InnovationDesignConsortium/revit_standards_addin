using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace RevitDataValidator
{
    public static class Utils
    {
        public static readonly string ALL = "<all>";
        public static readonly char LIST_SEP = ',';
        public static List<Rule> allRules;
        public static List<string> errors;

        private static readonly Dictionary<BuiltInCategory, List<BuiltInCategory>> CatToHostCatMap = new Dictionary<BuiltInCategory, List<BuiltInCategory>>()
    {
        { BuiltInCategory.OST_Doors, new List<BuiltInCategory> {BuiltInCategory.OST_Walls } },
        { BuiltInCategory.OST_Windows, new List<BuiltInCategory> {BuiltInCategory.OST_Walls, BuiltInCategory.OST_Roofs } },
    };

        public static Dictionary<string, BuiltInCategory> catMap = new Dictionary<string, BuiltInCategory>();

        public static List<BuiltInCategory> GetBuiltInCats(Rule rule)
        {
            if (rule.Categories.Count() == 1 && rule.Categories.First() == Utils.ALL)
            {
                return catMap.Values.ToList();
            }
            else
            {
                var builtInCats = rule.Categories.Select(q => catMap[q]).ToList();
                if (rule.RuleType == RuleType.FromHostInstance ||
                    rule.RuleType == RuleType.FromHostType)
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