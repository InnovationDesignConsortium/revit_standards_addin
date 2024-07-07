using System;
using System.Collections.ObjectModel;

namespace RevitDataValidator
{
    public class WorksetRuleData
    {
        public string WorksetName { get; set; }
        public ObservableCollection<ParameterData> WorksetRuleParameters { get; set; }
        public ObservableCollection<string> WorksetRuleCategories { get; set; }
        public string CategoryList
        { get { return string.Join(", ", WorksetRuleCategories); } }
        public Guid Guid { get; set; }
    }
}