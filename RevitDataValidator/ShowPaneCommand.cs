using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitDataValidator
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ShowPaneCommand : Nice3point.Revit.Toolkit.External.ExternalCommand
    {
        public override void Execute()
        {
            var pane = ExternalCommandData.Application.GetDockablePane(Utils.paneId);
            if (pane?.IsShown() != false)
                return;

            pane.Show();

            Update.CheckForUpdates();
        }
    }
}