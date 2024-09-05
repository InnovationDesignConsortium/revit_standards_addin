using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NLog.Config;

public class SetQuadrant
{
    public void Run(Document doc, List<ElementId> ids)
    {
		var logger = LogManager.GetCurrentClassLogger();
		logger.Info("This is an log message from custom code for Set Quadrant");
		
		// TaskDialog.Show("Test", "Showing task dialog integration in a custom rule for demo purposes");

        List<FamilyInstance> instances = null;
        if (ids == null)
        {
            instances = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();
        }
        else
        {
            instances = ids.Select(q => doc.GetElement(q)).Cast<FamilyInstance>().ToList();
        }

        var areas = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Areas).Cast<Area>();

        foreach (var area in areas)
        {
			// Logger.Info($"Analyzing area '{area.Name}'");
            var height = area.get_Parameter(BuiltInParameter.ROOM_COMPUTATION_HEIGHT).AsDouble();
            if (height == 0)
            {
                continue;
            }
            var options = new SpatialElementBoundaryOptions();
            var boundarySegments = area.GetBoundarySegments(options);
            var curveLoopList = new List<CurveLoop>();
            foreach (var loop in boundarySegments)
            {
                var cl = new CurveLoop();
                foreach (var boundarySegment in loop)
                {
                    cl.Append(boundarySegment.GetCurve());
                }
                curveLoopList.Add(cl);
            }
            var areaSolid = GeometryCreationUtilities.CreateExtrusionGeometry(curveLoopList, XYZ.BasisZ, height);
            foreach (var instance in instances)
            {
                var parameter = instance.LookupParameter("Containing Area");
                if (parameter == null)
                {
                    continue;
                }
                if (instance.Location is LocationPoint lp)
                {
                    var pt = lp.Point;
                    var sphere = CreateSphere(pt, 0.01);
                    var intersection = BooleanOperationsUtils.ExecuteBooleanOperation(sphere, areaSolid, BooleanOperationsType.Intersect);
                    if (intersection.Volume == 0)
                    {
                        continue;
                    }
                    parameter.Set(area.Name);
                }
            }
        }
    }
	
	//private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static Solid CreateSphere(XYZ xyz, double radius)
    {
        var frame = new Frame(xyz, XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ);

        var arc = Arc.Create(
           xyz - radius * XYZ.BasisZ,
           xyz + radius * XYZ.BasisZ,
           xyz + radius * XYZ.BasisX);

        var line = Line.CreateBound(
           arc.GetEndPoint(1),
           arc.GetEndPoint(0));

        var halfCircle = new CurveLoop();
        halfCircle.Append(arc);
        halfCircle.Append(line);

        var loops = new List<CurveLoop>(1)
        {
            halfCircle
        };

        return GeometryCreationUtilities.CreateRevolvedGeometry(frame, loops, 0, 2 * Math.PI);
    }
}