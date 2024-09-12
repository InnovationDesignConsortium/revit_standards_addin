using Autodesk.Revit.DB;
using System;
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

                var doc = data.GetDocument();
                var modifiedIds = data.GetModifiedElementIds().ToList();
                var addedIds = data.GetAddedElementIds().ToList();

                var addedAndModifiedIds = addedIds.ToList();
                addedAndModifiedIds.AddRange(modifiedIds);
                addedAndModifiedIds = addedAndModifiedIds.Where(q => !Utils.idsTriggered.Contains(q)).ToList();
                Utils.idsTriggered.AddRange(addedAndModifiedIds);

                Utils.RunAllRules(addedAndModifiedIds, WhenToRun.Realtime);
            
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
    }
}