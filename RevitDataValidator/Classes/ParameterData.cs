using System;

namespace RevitDataValidator
{
    public class ParameterData : IEquatable<ParameterData>
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return Name + " = " + Value;
        }

        public override bool Equals(object obj) => this.Equals(obj as ParameterData);

        public bool Equals(ParameterData other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Name == other.Name && Value == other.Value)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + Name.GetHashCode();
            hash = hash * 23 + Value.GetHashCode();
            return hash;
        }
    }
}