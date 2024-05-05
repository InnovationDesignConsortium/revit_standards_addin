using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitDataValidator
{
    public class WorksetRuleData
    {
        public string WorksetName { get; set; }
        public ObservableCollection<ParameterData> WorksetRuleParameters { get; set; }
        public ObservableCollection<string> WorksetRuleCategories { get; set; }
        public string CategoryList { get { return string.Join(", ", WorksetRuleCategories); } }
        public Guid Guid { get; set; }
    }
}
