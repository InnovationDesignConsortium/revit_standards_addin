namespace RevitDataValidator
{
    public interface IStateParameter
    { 
        string Name { get; set; }
        string State { get; }
        object Value { get; set; }
    }
}
