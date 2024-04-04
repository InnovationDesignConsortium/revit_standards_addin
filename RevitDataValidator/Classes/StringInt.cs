using System.Diagnostics;

namespace RevitDataValidator
{
    [DebuggerDisplay("{String} {Int}")]
    public class StringInt
    {
        public StringInt(string s, int i)
        {
            String = s;
            Int = i;
        }

        public string String { get; set; }
        public int Int { get; set; }
    }
}