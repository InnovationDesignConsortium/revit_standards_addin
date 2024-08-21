using System.Collections.Generic;

namespace RevitDataValidator
{
    public class StandardsConfig
    {
        public string PathToStandardsFiles { get; set; }
        public List<string> RvtFullPathRegex { get; set; }
    }

    public class GitRuleConfigRoot
    {
        public List<StandardsConfig> StandardsConfig { get; set; }
    }
}