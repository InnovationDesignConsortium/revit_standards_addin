using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RevitDataValidator
{
    internal class ParameterInfo
    {
        public List<Parameter> Parameters { get; set; }
        public StorageType StorageType {  get; set; }
        public List<string> Values { get; set; }
    }
}
