using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public abstract class StateParameter<T> : IStateParameter
    {
        public Parameter Parameter { get; set; }
        public object Value { get; set; }
        public string State { get; set; }
        public string Name { get; set; }
    }

    public class BoolStateParameter : StateParameter<bool>
    { }

    public class TextStateParameter : StateParameter<string>
    { }

    public class ChoiceStateParameter : StateParameter<object>
    {
        public List<string> Choices { get; set; }
        public string SelectedChoice { get; set; }
    }
}
