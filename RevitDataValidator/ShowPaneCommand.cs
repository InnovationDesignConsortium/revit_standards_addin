using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitDataValidator
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ShowPaneCommand : IExternalCommand
    {
        Result IExternalCommand.Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var pane = commandData.Application.GetDockablePane(Utils.paneId);
            if (pane == null || pane.IsShown())
                return Result.Cancelled;

            pane.Show();

            return Result.Succeeded;
        }
    }
}