using System;
using System.Collections.ObjectModel;

namespace RevitDataValidator
{
    public class ParameterRuleData
    {
        public string RuleName { get; set; }
        public ObservableCollection<string> ParameterRuleCategories { get; set; }

        public string RuleNameWithCategories
        {
            get { return $"{RuleName} ({string.Join(", ", ParameterRuleCategories)})"; }
        }

        public Guid Guid { get; set; }
    }
}