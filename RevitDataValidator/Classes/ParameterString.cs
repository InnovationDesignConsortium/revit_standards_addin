using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitDataValidator
{
    [DebuggerDisplay("{Name} {Value}")]
    public class ParameterString
    {
        public ParameterString(Parameter parameter, string value)
        {
            Parameter = parameter;
            Value = value;
        }

        public string Name => Parameter.Definition.Name;

        public Parameter Parameter { get; set; }
        public string Value { get; set; }
    }
}
