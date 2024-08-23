using Autodesk.Revit.Attributes;
using RevitDataValidator.Forms;

namespace RevitDataValidator
{
    [Transaction(TransactionMode.ReadOnly)]
    public class AboutCommand : Nice3point.Revit.Toolkit.External.ExternalCommand
    {
        public override void Execute()
        {
            using (var form = new frmAbout())
            {
                form.ShowDialog();
            }
        }
    }
}