using Autodesk.Revit.DB;
using RevitDataValidator.Forms;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace RevitDataValidator
{
    public class DataValidationUpdater : IUpdater
    {
        private UpdaterId updaterId;

        public DataValidationUpdater(AddInId id)
        {
            updaterId = new UpdaterId(id, new Guid("F1FAF6B3-4C06-42d4-97C1-D2B1EB593EFF"));
        }

        public void Execute(UpdaterData data)
        {
            try
            {
                if (Utils.dialogIdShowing == "Dialog_Revit_PartitionsEnable")
                    return;

                Document doc = data.GetDocument();
                var modifiedIds = data.GetModifiedElementIds().ToList();
                var addedIds = data.GetAddedElementIds().ToList();

                List<ElementId> addedAndModifiedIds = addedIds.ToList();
                addedAndModifiedIds.AddRange(modifiedIds);

                if (doc.IsWorkshared)
                {
                    foreach (var rule in Utils.allWorksetRules)
                    {
                        Utils.RunWorksetRule(rule, addedAndModifiedIds);
                    }
                }

                foreach (var rule in
                    Utils.allParameterRules
                    .Where(q =>
                        q.CustomCode != null &&
                        !Utils.CustomCodeRunning.Contains(q.CustomCode) &&
                        Utils.dictCustomCode.ContainsKey(q.CustomCode)))
                {
                    var ids = Utils.RunCustomRule(rule, addedAndModifiedIds);
                    if (ids.Any() && addedAndModifiedIds.Any(x => ids.Any(y => y == x)))
                    {
                        Utils.Log($"{rule.CustomCode}|Custom rule failed for elements [{string.Join(", ", ids.Select(q => Utils.GetElementInfo(doc.GetElement(q))))}]", Utils.LogLevel.Warn);
                        FailureMessage failureMessage = new FailureMessage(rule.FailureId);
                        failureMessage.SetFailingElements(ids.ToList());
                        if (doc.IsModifiable)
                        {
                            doc.PostFailure(failureMessage);
                        }
                    }

                    var ruleFailures = new List<RuleFailure>();

                    foreach (ElementId id in modifiedIds)
                    {
                        var failuresForThisRule = Utils.GetFailures(id, null, out List<ParameterString> parametersToSet);
                        ruleFailures.AddRange(failuresForThisRule);
                        foreach (var parameterString in parametersToSet)
                        {
                            SetParam(parameterString.Parameter, parameterString.NewValue);
                        }
                    }
                    if (ruleFailures.Count != 0)
                    {
                        FormGridList form = new FormGridList(ruleFailures);
                        form.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogException("DataValidationUpdater", ex);
            }
        }

        public string GetAdditionalInformation()
        { return "DataValidationUpdater"; }

        public ChangePriority GetChangePriority()
        { return ChangePriority.FloorsRoofsStructuralWalls; }

        public UpdaterId GetUpdaterId()
        { return updaterId; }

        public string GetUpdaterName()
        { return "DataValidationUpdater"; }

        private static void PostFailure(Document doc, ElementId id, FailureDefinitionId failureId)
        {
            FailureMessage failureMessage = new FailureMessage(failureId);
            failureMessage.SetFailingElement(id);
            doc.PostFailure(failureMessage);
        }

        private static string GetParamAsDoubleString(Parameter p)
        {
            if (p == null)
                return null;
            return p.AsDouble().ToString();
        }

        private static void SetParam(Parameter p, string s)
        {
            if (p == null)
                return;

            if (p.Definition.Name == "Type Name")
            {
                p.Element.Name = s;
                return;
            }

            if (p.IsReadOnly)
            {
                Utils.Log($"Parameter {p.Definition.Name} for element '{Utils.GetElementInfo(p.Element)}' is readonly", Utils.LogLevel.Error);
                return;
            }

            if (p.StorageType == StorageType.String)
            {
                p.Set(s);
            }
            else if (p.StorageType == StorageType.Integer)
            {
                if (int.TryParse(s, out int sInt))
                {
                    p.Set(sInt);
                }
                else if (double.TryParse(s, out double d))
                {
                    p.Set(Convert.ToInt32(d));
                }
                else if (s == "No")
                {
                    p.Set(0);
                }
                else if (s == "Yes")
                {
                    p.Set(1);
                }
            }
            else if (p.StorageType == StorageType.Double &&
                double.TryParse(s, out double d))
            {
                if (double.IsInfinity(d) ||
                    double.IsNaN(d))
                {
                    p.Set(0);
                }
                else
                {
                    p.Set(d);
                }
            }
        }
    }
}