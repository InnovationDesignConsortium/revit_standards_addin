using System.Collections.ObjectModel;

namespace RevitDataValidator
{
    public class PackData
    {
        public string ParameterName { get; set; }
        public ObservableCollection<IStateParameter> StateParametersList { get; set; }
    }
}