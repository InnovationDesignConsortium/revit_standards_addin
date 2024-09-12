using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitDataValidator.Classes
{
    public partial class RuleData
    {
        [JsonProperty("Parameter Rules")]
        public List<ParameterRule> ParameterRules { get; set; }

        [JsonProperty("Workset Rules")]
        public List<WorksetRule> WorksetRules { get; set; }
    }
}