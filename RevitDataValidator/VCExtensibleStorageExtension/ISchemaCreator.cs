using Autodesk.Revit.DB.ExtensibleStorage;
using System;

namespace VCExtensibleStorageExtension
{
    /// <summary>
    /// Create an Autodesk Extensible storage schema from a type
    /// </summary>
    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    public interface ISchemaCreator
    {
        Schema CreateSchema(Type type);
    }
}