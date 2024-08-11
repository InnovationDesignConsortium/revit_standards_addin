using System.Diagnostics;

namespace RevitDataValidator
{
    [DebuggerDisplay("{String} {Long}")]
    public class StringInt
    {
        public StringInt(string s, long l)
        {
            String = s;
            Long = l;
        }

        public string String { get; set; }
        public long Long { get; set; }
    }
}