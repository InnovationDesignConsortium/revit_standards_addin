using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public class PackSet
    {
        public string Name { get; set; }
        public string Category { get; set; }

        [JsonProperty("Parameter Packs")]
        public List<string> ParameterPacks { get; set; }

        [JsonProperty("Show All Other Parameters")]
        public bool ShowAllOtherParameters { get; set; }

        [JsonProperty("Show All Other Parameters Excluding")]
        public List<string> ShowAllOtherParametersExcluding { get; set; }
    }

    public class ParameterPack
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public List<string> Parameters { get; set; }
        public string URL { get; set; }
        public string PDF { get; set; }
        public string Video { get; set; }
        [JsonProperty("Custom Tools")]
        public List<string> CustomTools { get; set; }
    }

    public class ParameterUIData
    {
        [JsonProperty("Parameter Packs")]
        public List<ParameterPack> ParameterPacks { get; set; }

        [JsonProperty("Pack Sets")]
        public List<PackSet> PackSets { get; set; }
    }


}
