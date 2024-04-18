using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public partial class RuleData
    {
        [JsonProperty("Rules")]
        public List<Rule> Rules { get; set; }
    }

    public partial class Rule
    {
        public FailureDefinitionId FailureId { get; set; }
        [JsonProperty("Rule Name")]
        public string RuleName { get; set; }
        [JsonProperty("When Run")]
        public string WhenRun { get; set; }

        [JsonProperty("Revit File Names")]
        public List<string> RevitFileNames { get; set; }

        [JsonProperty("Categories")]
        public List<string> Categories { get; set; }

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
    }

    public partial class ListOption
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

    }
}