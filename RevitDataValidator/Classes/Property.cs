using System.Diagnostics;

namespace RevitDataValidator
{
    [DebuggerDisplay("{Name} {Value}")]
    public class Property
    {
        public Property(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; set; }
        public string Value { get; set; }
    }
}