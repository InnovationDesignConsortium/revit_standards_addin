using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitDataValidator
{
    public class EventHandlerWithParameterValue : RevitEventWrapper<ParameterValue>
    {
        public override void Execute(UIApplication uiApp, ParameterValue args)
        {
            using (Transaction t = new Transaction(Utils.doc, "Update Parameter"))
            {
                t.Start();
                foreach (var id in Utils.selectedIds)
                {
                    var element = Utils.doc.GetElement(id);
                    var parameter = element.LookupParameter(args.Parameter.Definition.Name);
                    if (parameter == null)
                    {
                        continue;
                    }
                    if (parameter.StorageType == StorageType.String)
                    {
                        parameter.Set(args.Value);
                    }
                    else if (parameter.StorageType == StorageType.Integer)
                    {
                        if (int.TryParse(args.Value, out int i))
                        {
                            parameter.Set(i);
                        }
                        else if (parameter.Definition.GetDataType() == SpecTypeId.Boolean.YesNo)
                        {
                            if (args.Value=="True")
                            {
                                parameter.Set(1);
                            }
                            else
                            {
                                parameter.Set(0);
                            }
                        }
                    }
                    else if (parameter.StorageType == StorageType.Double)
                    {
                        if (UnitFormatUtils.TryParse(Utils.doc.GetUnits(), parameter.Definition.GetDataType(), args.Value, out double d))
                        {
                            parameter.Set(d);
                        }
                    }
                }
                t.Commit();
            }
            Utils.propertiesPanel.Refresh();
        }
    }
}
