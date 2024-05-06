using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RevitDataValidator
{
    [DebuggerDisplay("{Name} {NewValue}")]
    public class ParameterObject
    {
        public ParameterObject(List<Parameter> parameters, object value)
        {
            Parameters = parameters;
            Value = value;
        }

        public string Name => Parameters.First().Definition.Name;

        public List<Parameter> Parameters { get; set; }
        public object Value { get; set; }
    }
}