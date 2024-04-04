using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public class EventHandlerCreateInstancesInRoom : RevitEventWrapper<List<ElementId>>
    {
        public override void Execute(UIApplication uiApp, List<ElementId> args)
        {
            using (Transaction t = new Transaction(Utils.doc, "Place Instances In Room"))
            {
                t.Start();
                foreach (ElementId roomId in Utils.selectedIds)
                {
                    if (Utils.doc.GetElement(roomId) is SpatialElement room)
                    {
                        var pt = ((LocationPoint)room.Location).Point;
                        foreach (ElementId id in args)
                        {
                            if (Utils.doc.GetElement(id) is FamilySymbol familySymbol)
                            {
                                if (!familySymbol.IsActive)
                                    familySymbol.Activate();

                                FamilyInstance instance = null;
                                try
                                {
                                    instance = Utils.doc.Create.NewFamilyInstance(pt, familySymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                }
                                catch (Exception ex)
                                {
                                }
                                if (instance == null)
                                {
                                    try
                                    {
                                        instance = Utils.doc.Create.NewFamilyInstance(pt, familySymbol, room.Level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                    }
                                    catch (Exception ex)
                                    {
                                    }
                                }
                            }
                        }
                    }
                }

                t.Commit();
            }
        }
    }
}