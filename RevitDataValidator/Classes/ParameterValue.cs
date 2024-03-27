using Autodesk.Revit.DB;
using System.Diagnostics;

namespace RevitDataValidator
{
    [DebuggerDisplay("{Name} {Value}")]
    public class ParameterValue
    {
        public ParameterValue(Parameter parameter, string value)
        {
            Parameter = parameter;
            Value = value;
        }

        public string Name => Parameter.Definition.Name;

        public Parameter Parameter { get; set; }
        public string Value { get; set; }
    }
}