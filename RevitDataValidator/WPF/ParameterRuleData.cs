using System;
using System.Collections.ObjectModel;

namespace RevitDataValidator
{
    public class ParameterRuleData
    {
        public string RuleName { get; set; }
        public ObservableCollection<string> ParameterRuleCategories { get; set; }
        public string CategoryList
        { get { return string.Join(", ", ParameterRuleCategories); } }
        public Guid Guid { get; set; }
    }
}