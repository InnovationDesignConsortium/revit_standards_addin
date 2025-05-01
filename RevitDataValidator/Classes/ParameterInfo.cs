using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitDataValidator
{
    internal class ParameterInfo
    {
        public List<Parameter> Parameters { get; set; }
        public StorageType StorageType {  get; set; }
        public List<string> Values { get; set; }
    }
}
