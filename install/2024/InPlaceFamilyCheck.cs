using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

public class InPlaceFamilyCheck
{
	public IEnumerable<ElementId> Run(Document doc)
	{
		var inplaceFamilies = new FilteredElementCollector(doc)
			.OfClass(typeof(FamilyInstance))
			.Cast<FamilyInstance>()
			.Where(q => q.Symbol.Family.IsInPlace);
			
		if (inplaceFamilies.Count() < 5)
			return null;
		else
			return inplaceFamilies.Select(q => q.Id);
	}
}