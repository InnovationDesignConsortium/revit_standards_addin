using Autodesk.Revit.DB;
using System.Diagnostics;

namespace RevitDataValidator
{
    [DebuggerDisplay("{Name} {Value}")]
    public class ParameterObject
    {
        public ParameterObject(Parameter parameter, object value)
        {
            Parameter = parameter;
            Value = value;
        }

        public string Name => Parameter.Definition.Name;

        public Parameter Parameter { get; set; }
        public object Value { get; set; }
    }
}