using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public class EventHandlerWithParameterObject : RevitEventWrapper<List<ParameterObject>>
    {
        public override void Execute(UIApplication uiApp, List<ParameterObject> parameterObjects)
        {
            try
            {
                using (Transaction t = new Transaction(Utils.doc, "Update Parameters"))
                {
                    t.Start();
                    foreach (var args in parameterObjects)
                    {
                        if (args.Parameters == null)
                            continue;

                        foreach (var parameter in args.Parameters)
                        {
                            if (parameter.IsReadOnly)
                            {
                                if (parameter.Definition.Name == "Type Name" &&
                                    args.Value is string typeNameValue)
                                {
                                    parameter.Element.Name = typeNameValue;
                                }
                                continue;
                            }

                            if (parameter.StorageType == StorageType.String &&
                                args.Value is string s)
                            {
                                parameter.Set(s);
                            }
                            else if (parameter.StorageType == StorageType.Integer)
                            {
                                var dataType = parameter.Definition.GetDataType();
                                if (dataType == SpecTypeId.Int.Integer)
                                {
                                    if (int.TryParse(args.Value.ToString(), out int i))
                                    {
                                        parameter.Set(i);
                                    }
                                    else
                                    {
                                        TaskDialog.Show("Error", "Enter a valid integer");
                                    }
                                }
                                else if (dataType == SpecTypeId.Boolean.YesNo &&
                                    args.Value is string ss)
                                {
                                    if (ss == "True")
                                    {
                                        parameter.Set(1);
                                    }
                                    else
                                    {
                                        parameter.Set(0);
                                    }
                                }
                                else
                                {
                                    if (args.Value is StringInt si &&
                                        int.TryParse(si.Int.ToString(), out int i))
                                    {
                                        parameter.Set(i);
                                    }
                                }
                            }
                            else if (parameter.StorageType == StorageType.Double)
                            {
                                if (UnitFormatUtils.TryParse(Utils.doc.GetUnits(), parameter.Definition.GetDataType(), args.Value.ToString(), out double dparsed, out string parseFailureMessage))
                                {
                                    parameter.Set(dparsed);
                                }
                                else
                                {
                                    TaskDialog.Show("Error", parseFailureMessage);
                                }
                            }
                            else if (parameter.StorageType == StorageType.ElementId &&
                                args.Value is StringInt si &&
                                int.TryParse(si.Int.ToString(), out int i))
                            {
                                var elementid = new ElementId(i);
                                try
                                {
                                    bool didSet = parameter.Set(elementid);
                                    if (!didSet)
                                    {
                                        TaskDialog.Show("Error", $"Unable to set {parameter.Definition.Name} to {Utils.doc.GetElement(elementid).Name}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Utils.LogException("EventHandlerWithParameterObject setting parameter", ex);
                                }
                            }
                        }
                    }
                    t.Commit();
                }
                Utils.propertiesPanel.Refresh();
            }
            catch (Exception ex)
            {
                Utils.LogException("EventHandlerWithParameterObject", ex);
            }
        }
    }
}