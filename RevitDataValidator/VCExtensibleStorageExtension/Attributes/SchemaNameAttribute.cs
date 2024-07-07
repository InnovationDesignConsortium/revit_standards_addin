using Autodesk.Revit.DB.ExtensibleStorage;
using System;

namespace VCExtensibleStorageExtension.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class SchemaAttribute : Attribute
    {
        private readonly string _schemaName;
        private readonly Guid _guid;

        public SchemaAttribute(string guid, string schemaName)
        {
            _schemaName = schemaName;
            _guid = new Guid(guid);
        }

        public string SchemaName
        {
            get { return _schemaName; }
        }

        public string ApplicationGUID { get; set; }

        public string Documentation { get; set; }

        public Guid GUID
        {
            get { return _guid; }
        }

        public AccessLevel ReadAccessLevel { get; set; }

        public AccessLevel WriteAccessLevel { get; set; }

        public string VendorId { get; set; }
    }
}