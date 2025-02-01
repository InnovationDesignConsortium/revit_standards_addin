using Autodesk.Revit.Attributes;

namespace RevitDataValidator
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ReloadRulesCommand : Nice3point.Revit.Toolkit.External.ExternalCommand
    {
        public override void Execute()
        {
          Utils.ReloadRules(true);
        }
    }
}