using Autodesk.Revit.DB;
using System.Diagnostics;

namespace RevitDataValidator
{
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