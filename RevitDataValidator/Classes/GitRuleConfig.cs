using System.Collections.Generic;

namespace RevitDataValidator
{
    public class GitRuleConfig
    {
        public string PathToRuleFile { get; set; }
        public List<string> RvtFullPathRegex { get; set; }
    }

    public class GitRuleConfigRoot
    {
        public List<GitRuleConfig> GitRuleConfig { get; set; }
    }
}