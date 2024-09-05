using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Async.ExternalEvents;
using System;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public class CustomRuleExternalEventHandler :
    SyncGenericExternalEventHandler<ParameterRule, IEnumerable<ElementId>>
    {
        protected override IEnumerable<ElementId> Handle(UIApplication app, ParameterRule rule)
        {
            try
            {
                IEnumerable<ElementId> ids = new List<ElementId>();
                using (Transaction t = new Transaction(Utils.doc, rule.CustomCode))
                {
                    var started = false;
                    if (!Utils.doc.IsModifiable)
                    {
                        t.Start();
                        started = true;
                    }
                    ids = Utils.RunCustomRule(rule, null);
                    if (started)
                    {
                        t.Commit();
                    }
                    return ids;
                }
            }
            catch (Exception ex)
            {
                Utils.LogException($"Exception thrown running custom rule: '{rule.RuleName}'", ex);
                return new List<ElementId>();
            }
        }

        public override object Clone()
        {
            throw new NotImplementedException();
        }

        public override string GetName()
        {
            return "CustomRuleExternalEventHandler";
        }
    }
}