using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Diagnostics;

namespace RevitDataValidator
{
    [DebuggerDisplay("{Name} {Value}")]
    public class ParameterObject
    {
        public ParameterObject(List<Parameter> parameters, object value)
        {
            Parameters = parameters;
            Value = value;
        }

        public string Name => Parameters[0].Definition.Name;

        public List<Parameter> Parameters { get; set; }
        public object Value { get; set; }
    }
}