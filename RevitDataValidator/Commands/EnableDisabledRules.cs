using Autodesk.Revit.Attributes;
using RevitDataValidator.Forms;

namespace RevitDataValidator
{
    [Transaction(TransactionMode.ReadOnly)]
    public class EnableDisabledRules : Nice3point.Revit.Toolkit.External.ExternalCommand
    {
        public override void Execute()
        {
            using (var form = new FormEnableDisabledRules())
            {
                form.ShowDialog();
            }
        }
    }
}