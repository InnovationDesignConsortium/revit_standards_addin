using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitDataValidator
{
    public class ParameterRuleData
    {
        public string RuleName { get; set; }
        public ObservableCollection<string> ParameterRuleCategories { get; set; }
        public string CategoryList { get { return string.Join(", ", ParameterRuleCategories); } }
        public Guid Guid { get; set; }
    }
}
