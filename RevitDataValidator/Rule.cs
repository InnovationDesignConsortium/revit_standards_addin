using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public class Rule
    {
        public List<string> Categories { get; set; }
        public string ParameterName { get; set; }
        public RuleType RuleType { get; set; }
        public string RuleData { get; set; }
        public string UserMessage { get; set; }
        public FailureDefinitionId FailureId { get; set; }
        public string DocumentPath { get; set; }
        public bool IsRequired { get; set; }

        public override string ToString()
        {
            return $"{ParameterName} {RuleType} {RuleData}";
        }
    }
}