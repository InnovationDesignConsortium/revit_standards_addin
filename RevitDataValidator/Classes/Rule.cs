using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;

namespace RevitDataValidator
{
    public partial class RuleData
    {
        [JsonProperty("Parameter Rules")]
        public List<ParameterRule> ParameterRules { get; set; }

        [JsonProperty("Workset Rules")]
        public List<WorksetRule> WorksetRules { get; set; }
    }

    public interface Rule
    {
        List<string> RevitFileNames { get; set; }
        List<string> Categories { get; set; }
    }

    [DebuggerDisplay("{Categories} {ParameterName}")]
    public partial class ParameterRule : Rule
    {
        [JsonProperty("Revit File Names")]
        public List<string> RevitFileNames { get; set; }

        [JsonProperty("Categories")]
        public List<string> Categories { get; set; }
        public FailureDefinitionId FailureId { get; set; }
        [JsonProperty("Rule Name")]
        public string RuleName { get; set; }
        [JsonProperty("When Run")]
        public string WhenRun { get; set; }

        [JsonProperty("Parameter Name")]
        public string ParameterName { get; set; }

        [JsonProperty("List Options")]
        public List<ListOption> ListOptions { get; set; }

        [JsonProperty("Requirement")]
        public string Requirement { get; set; }

        public string Regex { get; set; }

        [JsonProperty("Prevent Duplicates")]
        public string PreventDuplicates { get; set; }

        [JsonProperty("User Message")]
        public string UserMessage { get; set; }

        [JsonProperty("From Host Instance")]
        public string FromHostInstance { get; set; }

        [JsonProperty("Element Classes")]
        public List<string> ElementClasses { get; set; }

        [JsonProperty("Custom Code")]
        public string CustomCode { get; set; }

        public string Formula { get; set; }

        [JsonProperty("Driven Parameters")]
        public List<string> DrivenParameters { get; set; }

        [JsonProperty("Key Values")]
        public List<List<string>> KeyValues { get; set; }

        public string Format { get; set; }

        public override string ToString()
        {
            return RuleName;
        }
    }

    public partial class ListOption
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

    }

    public class ParameterData
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class WorksetRule : Rule
    {
        [JsonProperty("Revit File Names")]
        public List<string> RevitFileNames { get; set; }

        [JsonProperty("Categories")]
        public List<string> Categories { get; set; }
        public string Workset { get; set; }
        public List<ParameterData> Parameters { get; set; }
    }

    public enum FailureType
    {
        INVALID,
        List,
        Regex
    }

    public class RuleFailure
    {
        public ParameterRule Rule { get; set; }
        public FailureType FailureType { get; set;}
        public ElementId ElementId { get; set; }
    }
}