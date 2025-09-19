using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitDataValidator
{
    public class EventHandlerWithParameterObject : RevitEventWrapper<List<ParameterObject>>
    {
        public override void Execute(UIApplication uiApp, List<ParameterObject> parameterObjects)
        {
            try
            {
                if (Utils.doc == null)
                {
                    return;
                }

                using (Transaction t = new Transaction(Utils.doc, "Update Parameters"))
                {
                    t.Start();
                    foreach (var args in parameterObjects)
                    {
                        if (args.Parameters == null)
                            continue;

                        if (args.Value is string argValueString && double.TryParse(argValueString, out double d))
                        {
                            if (double.IsInfinity(d))
                            {
                                Utils.Log($"Value is not finite so cannot set {string.Join(",", args.Parameters.Select(q => q.Definition.Name))}", LogLevel.Error);
                                continue;
                            }
                            if (double.IsNaN(d))
                            {
                                Utils.Log($"Value is not a number so cannot set {string.Join(",", args.Parameters.Select(q => q.Definition.Name))}", LogLevel.Error);
                                continue;
                            }
                        }

                        foreach (var parameter in args.Parameters)
                        {
                            if (parameter.IsReadOnly)
                            {
                                if (parameter.Definition.Name == "Type Name" &&
                                    args.Value is string typeNameValue)
                                {
                                    parameter.Element.Name = typeNameValue;
                                }
                                else if (parameter.Definition.Name == "Family Name")
                                {
                                    if (args.Value is string familyName)
                                    {
                                        if (parameter.Element is FamilySymbol fs &&
                                            fs.Family.Name != familyName)
                                        {
                                            fs.Family.Name = familyName;
                                        }
                                        else if (parameter.Element is Family f &&
                                            f.Name != familyName)
                                        {
                                            f.Name = familyName;
                                        }
                                        else if (parameter.Element is FamilyInstance fi &&
                                            fi.Symbol.Family.Name != familyName)
                                        {
                                            fi.Symbol.Family.Name = familyName;
                                        }
                                    }
                                }
                                continue;
                            }

                            if (parameter.StorageType == StorageType.String)
                            {
                                if (args.Value is StringInt stringInt)
                                {
                                    parameter.Set(stringInt.String);
                                }
                                else if (args.Value is string s && parameter.AsString() != s)
                                {
                                    parameter.Set(s);
                                }
                            }
                            else if (parameter.StorageType == StorageType.Integer)
                            {
                                var dataType = parameter.Definition.GetDataType();
                                if (dataType == SpecTypeId.Int.Integer)
                                {
                                    if (int.TryParse(args.Value.ToString(), out int i) &&
                                        i != parameter.AsInteger())
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
                                        int.TryParse(si.Long.ToString(), out int i) &&
                                        i != parameter.AsInteger())
                                    {
                                        parameter.Set(i);
                                    }
                                }
                            }
                            else if (parameter.StorageType == StorageType.Double && args.Value.ToString() != "")
                            {
                                if (UnitFormatUtils.TryParse(Utils.doc.GetUnits(), parameter.Definition.GetDataType(), args.Value.ToString(), out double dparsed, out string parseFailureMessage))
                                {
                                    if (Math.Abs(parameter.AsDouble() - dparsed) > Utils.eps)
                                    {
                                        parameter.Set(dparsed);
                                    }
                                }
                                else
                                {
                                    TaskDialog.Show("Error", parseFailureMessage);
                                }
                            }
                            else if (parameter.StorageType == StorageType.ElementId &&
                                args.Value is StringInt si &&
                                int.TryParse(si.Long.ToString(), out int i))
                            {
                                var elementid = ElementIdUtils.New(i);
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