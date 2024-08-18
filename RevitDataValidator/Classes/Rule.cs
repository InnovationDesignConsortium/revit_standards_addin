using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
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
        List<string> Categories { get; set; }
        Guid Guid { get; set; }
    }

    [DebuggerDisplay("{Categories} {ParameterName}")]
    public partial class ParameterRule : Rule
    {

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
        public Guid Guid { get; set; }

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

        public override string ToString()
        {
            return Name;
        }
    }

    public class ParameterData : IEquatable<ParameterData>
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return Name + " = " + Value;
        }

        public override bool Equals(object obj) => this.Equals(obj as ParameterData);

        public bool Equals(ParameterData other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Name == other.Name && Value == other.Value)
            {
                return true;
            }
            else
            { 
                return false; 
            }
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + Name.GetHashCode();
            hash = hash * 23 + Value.GetHashCode();
            return hash;
        }
    }

    public class WorksetRule : Rule
    {

        [JsonProperty("Categories")]
        public List<string> Categories { get; set; }

        public string Workset { get; set; }
        public List<ParameterData> Parameters { get; set; }

        public Guid Guid { get; set; }

        public override string ToString()
        {
            return $"'{Workset}' [{string.Join(",", Categories)}] [{string.Join(",", Parameters)}]";
        }
    }

    public enum FailureType
    {
        INVALID,
        List,
        Regex,
        IfThen,
        PreventDuplicates
    }

    public class RuleFailure
    {
        public ParameterRule Rule { get; set; }
        public FailureType FailureType { get; set; }
        public ElementId ElementId { get; set; }
    }
}