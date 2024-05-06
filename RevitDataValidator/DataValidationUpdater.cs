﻿using Autodesk.Revit.DB;
using RevitDataValidator.Forms;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

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

                foreach (var rule in Utils.allWorksetRules)
                {
                    Utils.RunWorksetRule(rule, addedAndModifiedIds);
                }

                var applicableParameterRules = Utils.GetApplicableParameterRules();
                if (applicableParameterRules.Any())
                {
                    foreach (var rule in applicableParameterRules.Where(q => q.CustomCode != null && Utils.dictCustomCode.ContainsKey(q.CustomCode)))
                    {
                        var ids = Utils.RunCustomRule(rule);
                        if (ids.Any() && addedAndModifiedIds.Any(x => ids.Any(y => y == x)))
                        {
                            FailureMessage failureMessage = new FailureMessage(rule.FailureId);
                            failureMessage.SetFailingElements(ids);
                            if (doc.IsModifiable)
                            {
                                doc.PostFailure(failureMessage);
                            }
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
                    if (ruleFailures.Any())
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

            Utils.Log($"Set {p.Definition.Name} = {s}");

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