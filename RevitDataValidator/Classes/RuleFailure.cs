using Autodesk.Revit.DB;

namespace RevitDataValidator
{
    public class RuleFailure
    {
        public ParameterRule Rule { get; set; }
        public FailureType FailureType { get; set; }
        public ElementId ElementId { get; set; }
    }
}