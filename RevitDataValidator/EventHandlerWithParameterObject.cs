﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace RevitDataValidator
{
    public class EventHandlerWithParameterObject : RevitEventWrapper<ParameterObject>
    {
        public override void Execute(UIApplication uiApp, ParameterObject args)
        {
            try
            {
                if (args.Parameters == null)
                    return;

                using (Transaction t = new Transaction(Utils.doc, "Update Parameters"))
                {
                    t.Start();
                    foreach (var parameter in args.Parameters)
                    {
                        if (parameter.StorageType == StorageType.String &&
                            args.Value is string s)
                        {
                            parameter.Set(s);
                        }
                        else if (parameter.StorageType == StorageType.Integer)
                        {
                            if (args.Value is int i)
                            {
                                parameter.Set(i);
                            }
                            else if (parameter.Definition.GetDataType() == SpecTypeId.Boolean.YesNo &&
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
                        }
                        else if (parameter.StorageType == StorageType.Double)
                        {
                            if (UnitFormatUtils.TryParse(Utils.doc.GetUnits(), parameter.Definition.GetDataType(), args.Value.ToString(), out double dparsed))
                            {
                                parameter.Set(dparsed);
                            }
                        }
                        else if (parameter.StorageType == StorageType.ElementId &&
                            args.Value is int i)
                        {
                            var elementid = new ElementId(i);
                            try
                            {
                                bool didSet = parameter.Set(elementid);
                            }
                            catch (Exception ex)
                            {
                                Utils.LogException("EventHandlerWithParameterObject setting parameter", ex);
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