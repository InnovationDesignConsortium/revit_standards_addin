using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RevitDataValidator
{
    [DebuggerDisplay("{Categories} {ParameterName}")]
    public partial class ParameterRule : BaseRule
    {
        public FailureDefinitionId FailureId { get; set; }

        [JsonProperty("Parameter Name")]
        public string ParameterName { get; set; }

        [JsonProperty("List Options")]
        public List<ListOption> ListOptions { get; set; }

        [JsonProperty("List Source")]
        public string ListSource { get; set; }

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

        public Dictionary<string, List<List<string>>> DictKeyValues { get; set; }

        [JsonProperty("Key Values")]
        public List<List<string>> KeyValues { get; set; }

        [JsonProperty("Key Value Path")]
        public string KeyValuePath { get; set; }

        [JsonProperty("Filter Parameter")]
        public string FilterParameter { get; set; }

        public string Format { get; set; }

        [JsonProperty("Is Value Required")]
        public bool IsValueRequired { get; set; }

        public Guid FailureGuid { get; set; }

        public override string ToString()
        {
            return RuleName;
        }
    }
}