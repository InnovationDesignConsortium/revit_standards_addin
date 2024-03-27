using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitDataValidator
{
    public class EventHandlerWithProperty : RevitEventWrapper<Property>
    {
        public override void Execute(UIApplication uiApp, Property args)
        {
            using (Transaction t = new Transaction(Utils.doc, "Update Parameter"))
            {
                t.Start();
                foreach (var id in Utils.selectedIds)
                {
                    var element = Utils.doc.GetElement(id);
                    var parameter = element.LookupParameter(args.Name);
                    if (parameter == null)
                    {
                        continue;
                    }
                    if (parameter.StorageType == StorageType.String)
                    {
                        parameter.Set(args.Value);
                    }
                }
                t.Commit();
            }
        }
    }
}
