using Autodesk.Revit.DB.ExtensibleStorage;

namespace VCExtensibleStorageExtension
{
    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    public interface IEntityConverter
    {
        Entity Convert(IRevitEntity revitEntity);

        TRevitEntity Convert<TRevitEntity>(Entity entity) where TRevitEntity : class, IRevitEntity;
    }
}