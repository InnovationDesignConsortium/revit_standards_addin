using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;

namespace RevitDataValidator
{
    public class PropertyViewModel
    {
        private static readonly string OTHER = "Others";
        public ObservableCollection<PackData> PackData { get; set; }
        public ObservableCollection<string> cboData { get; set; }

        public PropertyViewModel()
        {
            cboData = new ObservableCollection<string>(Utils.parameterUIData.PackSets.Select(q => q.Name));
        }

        public PropertyViewModel(string name)
        {
            if (name == null)
            {
                PackData = new ObservableCollection<PackData>();
                return;
            }
            Element element = null;
            if (Utils.selectedIds.Any())
            {
                element = Utils.doc.GetElement(Utils.selectedIds.First());
            }
            else
            {
                element = Utils.doc.ActiveView;
            }

            if (element == null || element.Category == null)
                return;

            var catName = element.Category.Name;

            cboData = new ObservableCollection<string>(
                Utils.parameterUIData.PackSets
                .Where(q => q.Category == catName).Select(q => q.Name));

            var packSet = Utils.parameterUIData.PackSets.FirstOrDefault(q => q.Name == name);
            if (packSet.ParameterPacks == null)
                packSet.ParameterPacks = new List<string>();

            var packNames = packSet.ParameterPacks;
            var allPacks = Utils.parameterUIData.ParameterPacks.Where(q => packNames != null && packNames.Contains(q.Name));
            var allParameterNames = allPacks.SelectMany(q => q.Parameters).ToList();

            var packOthers = new ParameterPack
            {
                Name = OTHER,
                Parameters = element.Parameters
                .Cast<Parameter>()
                .Where(q => !allParameterNames.Contains(q.Definition.Name))
                .Where(q => packSet.ShowAllOtherParametersExcluding == null || !packSet.ShowAllOtherParametersExcluding.Contains(q.Definition.Name))
                .Where(q => q.Definition.ParameterGroup != BuiltInParameterGroup.INVALID && q.StorageType != StorageType.None)
                .OrderBy(q => q.Definition.Name)
                .Select(q => q.Definition.Name).ToList()
            };
            if (!packSet.ParameterPacks.Contains(OTHER) &&
                packSet.ShowAllOtherParameters)
            {
                packNames.Add(packOthers.Name);
            }

            PackData = new ObservableCollection<PackData>();

            foreach (var packName in packNames)
            {
                var parameterPack = Utils.parameterUIData.ParameterPacks.FirstOrDefault(q => q.Name == packName);
                if (packName == OTHER)
                    parameterPack = packOthers;

                if (parameterPack == null)
                {
                    Utils.Log($"{packName} is listed in {packSet.Name} but does not exist", Utils.LogLevel.Error);
                    continue;
                }

                var packParameters = new ObservableCollection<IStateParameter>();
                foreach (string pname in parameterPack.Parameters)
                {
                    var parameters = GetParameter(pname);
                    bool foundRule = false;
                    foreach (var rule in Utils.allParameterRules)
                    {
                        if (rule.Categories != null &&
                            rule.Categories.Contains(parameterPack.Category) &&
                            (rule.ListOptions != null || rule.KeyValues != null) &&
                            rule.ParameterName == pname)
                        {
                            List<StringInt> choices;
                            if (rule.ListOptions != null)
                            {
                                choices = rule.ListOptions.Select(q => new StringInt(q.Name, 0)).ToList();
                            }
                            else
                            {
                                choices = rule.KeyValues.Select(q => new StringInt(q[0], 0)).ToList();
                            }
                            var paramValue = GetParameterValue(pname);
                            var selected = choices.FirstOrDefault(q => q.String == paramValue);
                            packParameters.Add(new ChoiceStateParameter
                            {
                                Parameters = parameters,
                                Name = pname,
                                Choices = choices,
                                SelectedChoice = selected,
                                IsEnabled = !parameters.First().IsReadOnly
                            });
                            foundRule = true;
                            break;
                        }
                    }
                    if (!foundRule)
                    {
                        if (parameters != null && parameters.Count() > 0 && parameters.First() != null)
                        {
                            var parameter = parameters.First();
                            var value = GetParameterValue(pname);
                            if (parameter.StorageType == StorageType.Integer &&
                                parameter.Definition.GetDataType() == SpecTypeId.Boolean.YesNo)
                            {
                                var boolValue = false;
                                if (value == "Yes")
                                    boolValue = true;

                                packParameters.Add(
                                   new BoolStateParameter
                                   {
                                       Name = pname,
                                       Parameters = parameters,
                                       IsEnabled = !parameters.First().IsReadOnly,
                                       Value = boolValue
                                   }
                                   );
                            }
                            else if (parameter.StorageType == StorageType.Integer &&
                                parameter.AsValueString() != parameter.AsInteger().ToString())
                            {
                                var choices = new List<StringInt>();
                                if (parameter.GetTypeId() == ParameterTypeId.WallKeyRefParam)
                                {
                                    foreach (var v in Enum.GetValues(typeof(WallLocationLine)))
                                    {
                                        choices.Add(new StringInt(AddSpacesToSentence(v.ToString(), true), (int)v));
                                    }
                                }

                                if (choices.Any())
                                {
                                    var selected = choices.FirstOrDefault(q => q.Int == parameter.AsInteger());
                                    packParameters.Add(new ChoiceStateParameter
                                    {
                                        Parameters = parameters,
                                        Name = pname,
                                        IsEnabled = !parameters.First().IsReadOnly,
                                        Choices = choices,
                                        SelectedChoice = selected
                                    });
                                }
                            }
                            else if (parameter.StorageType == StorageType.ElementId)
                            {
                                if (parameter.Definition is InternalDefinition id)
                                {
                                    ForgeTypeId typeid = null;
                                    try
                                    {
                                        typeid = id.GetParameterTypeId();
                                    }
                                    catch
                                    { }
                                    List<StringInt> choices = new List<StringInt>();
                                    if (typeid == null)
                                    {
                                        if (parameter.Definition.GetDataType() == SpecTypeId.Reference.Material)
                                        {
                                            choices = GetChoices(typeof(Material));
                                            choices.Add(new StringInt("<By Category>", -1));
                                        }
                                        else
                                        {
                                            var keySchedule = new FilteredElementCollector(Utils.doc)
                                                .OfClass(typeof(ViewSchedule))
                                                .Cast<ViewSchedule>()
                                                .FirstOrDefault(q =>
                                                    q.Definition.IsKeySchedule &&
                                                    q.KeyScheduleParameterName == pname);
                                            if (keySchedule != null)
                                            {
                                                var scheduleElements = new FilteredElementCollector(Utils.doc, keySchedule.Id)
                                                    .ToElements()
                                                    .Select(q => new StringInt(q.Name, q.Id.IntegerValue))
                                                    .OrderBy(q => q.String)
                                                    .ToList();
                                                choices = scheduleElements;
                                                choices.Add(new StringInt("(none)", -1));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (typeid == ParameterTypeId.PhaseCreated ||
                                            typeid == ParameterTypeId.PhaseDemolished)
                                        {
                                            choices = GetChoices(typeof(Phase));

                                            if (typeid == ParameterTypeId.PhaseDemolished)
                                            {
                                                choices.Add(new StringInt("None", -1));
                                            }
                                        }
                                        else if (
                                            typeid == ParameterTypeId.AssociatedLevel ||
                                            typeid == ParameterTypeId.CurveBottomLevel ||
                                            typeid == ParameterTypeId.CurveLevel ||
                                            typeid == ParameterTypeId.CurveTopLevel ||
                                            typeid == ParameterTypeId.DpartBaseLevel ||
                                            typeid == ParameterTypeId.FabricationLevelParam ||
                                            typeid == ParameterTypeId.FaceroofLevelParam ||
                                            typeid == ParameterTypeId.FamilyBaseLevelParam ||
                                            typeid == ParameterTypeId.FamilyTopLevelParam ||
                                            typeid == ParameterTypeId.FamilyLevelParam ||
                                            typeid == ParameterTypeId.GroupLevel ||
                                            typeid == ParameterTypeId.ImportBaseLevel ||
                                            typeid == ParameterTypeId.InstanceReferenceLevelParam ||
                                            typeid == ParameterTypeId.InstanceScheduleOnlyLevelParam ||
                                            typeid == ParameterTypeId.LegendComponentDetailLevel ||
                                            typeid == ParameterTypeId.LevelParam ||
                                            typeid == ParameterTypeId.LevelUpToLevel ||
                                            typeid == ParameterTypeId.MultistoryStairsRefLevel ||
                                            typeid == ParameterTypeId.PlanViewLevel ||
                                            typeid == ParameterTypeId.RbsEndLevelParam ||
                                            typeid == ParameterTypeId.RbsStartLevelParam ||
                                            typeid == ParameterTypeId.RoofBaseLevelParam ||
                                            typeid == ParameterTypeId.RoofUptoLevelParam ||
                                            typeid == ParameterTypeId.RoofConstraintLevelParam ||
                                            typeid == ParameterTypeId.RoomLevelId ||
                                            typeid == ParameterTypeId.RoomUpperLevel ||
                                            typeid == ParameterTypeId.ScheduleBaseLevelParam ||
                                            typeid == ParameterTypeId.ScheduleLevelParam ||
                                            typeid == ParameterTypeId.ScheduleTopLevelParam ||
                                            typeid == ParameterTypeId.SlopeArrowLevelEnd ||
                                            typeid == ParameterTypeId.SlopeArrowLevelStart ||
                                            typeid == ParameterTypeId.SpaceReferenceLevelParam ||
                                            typeid == ParameterTypeId.StairsBaseLevelParam ||
                                            typeid == ParameterTypeId.StairsMultistoryTopLevelParam ||
                                            typeid == ParameterTypeId.StairsMultistoryUpToLevel ||
                                            typeid == ParameterTypeId.StairsTopLevelParam ||
                                            typeid == ParameterTypeId.StairsRailingBaseLevelParam ||
                                            typeid == ParameterTypeId.StructuralAttachmentEndLevelReference ||
                                            typeid == ParameterTypeId.StructuralAttachmentStartLevelReference ||
                                            typeid == ParameterTypeId.TrussElementReferenceLevelParam ||
                                            typeid == ParameterTypeId.ViewUnderlayBottomId ||
                                            typeid == ParameterTypeId.ViewUnderlayTopId ||
                                            typeid == ParameterTypeId.WallBaseConstraint ||
                                            typeid == ParameterTypeId.WallHeightType
                                            )
                                        {
                                            choices = GetChoices(typeof(Level));

                                            if (typeid == ParameterTypeId.StairsTopLevelParam ||
                                                typeid == ParameterTypeId.RoofUptoLevelParam ||
                                                typeid == ParameterTypeId.ViewUnderlayBottomId
                                                )
                                            {
                                                choices.Add(new StringInt("None", -1));
                                            }
                                            else if (typeid == ParameterTypeId.WallHeightType)
                                            {
                                                choices.Add(new StringInt("Unconnected", -1));
                                            }
                                            else if (typeid == ParameterTypeId.ViewUnderlayTopId)
                                            {
                                                choices.Add(new StringInt("Unbounded", -1));
                                            }
                                        }
                                        else if (typeid == ParameterTypeId.ViewPhaseFilter)
                                        {
                                            choices = GetChoices(typeof(PhaseFilter));
                                        }
                                        else if (typeid == ParameterTypeId.ViewTemplateForSchedule &&
                                            element is View view)
                                        {
                                            choices = new FilteredElementCollector(Utils.doc)
                                                .OfClass(typeof(View))
                                                .Cast<View>()
                                                .Where(q => q.IsTemplate && q.ViewType == view.ViewType)
                                                .Select(q => new StringInt(q.Name, q.Id.IntegerValue))
                                                .OrderBy(q => q.String).ToList();
                                            choices.Add(new StringInt("<None>", -1));
                                        }
                                        else if (typeid == ParameterTypeId.DesignOptionId)
                                        {
                                            choices = GetChoices(typeof(DesignOption));
                                            choices.Add(new StringInt("Main Model", -1));
                                        }
                                        else if (typeid == ParameterTypeId.ViewerVolumeOfInterestCrop)
                                        {
                                            choices = GetChoices(BuiltInCategory.OST_VolumeOfInterest);
                                            choices.Add(new StringInt("<None>", -1));
                                        }
                                    }
                                    if (choices.Any())
                                    {
                                        try
                                        {
                                            StringInt selected = null;
                                            if (parameter.StorageType == StorageType.ElementId &&
                                                value != string.Empty)
                                            {
                                                selected = choices.FirstOrDefault(q => q.Int == parameter.AsElementId().IntegerValue);
                                            }
                                            else
                                            {
                                                selected = choices.FirstOrDefault(q => q.String == value);
                                            }
                                            packParameters.Add(new ChoiceStateParameter
                                            {
                                                Parameters = parameters,
                                                IsEnabled = !parameters.First().IsReadOnly,
                                                Name = pname,
                                                Choices = choices,
                                                SelectedChoice = selected
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            Utils.LogException("Buidling ElementId choices", ex);
                                        }
                                    }
                                }
                            }
                            else if (parameter.GetTypeId() != ParameterTypeId.SymbolNameParam)
                            {
                                packParameters.Add(
                                    new TextStateParameter
                                    {
                                        Name = pname,
                                        Parameters = parameters,
                                        IsEnabled = !parameters.First().IsReadOnly,
                                        Value = value
                                    }
                                    );
                            }
                        }
                    }
                }

                if (parameterPack.CustomTools != null)
                {
                    foreach (var customToolName in parameterPack.CustomTools)
                    {
                        packParameters.Add(
                        new PushButtonParameter
                        {
                            Name = customToolName,
                        }
                        );
                    }
                }

                PackData.Add(new PackData
                {
                    PackName = parameterPack.Name,
                    PackParameters = packParameters,
                    LinkURL = parameterPack.URL,
                    PdfPath = parameterPack.PDF
                });
            }
        }

        private List<StringInt> GetChoices(Type t)
        {
            return new FilteredElementCollector(Utils.doc)
                        .OfClass(t)
                        .Select(q => new StringInt(q.Name, q.Id.IntegerValue))
                        .OrderBy(q => q.String).ToList();
        }

        private List<StringInt> GetChoices(BuiltInCategory bic)
        {
            return new FilteredElementCollector(Utils.doc)
                        .OfCategory(bic)
                        .Select(q => new StringInt(q.Name, q.Id.IntegerValue))
                        .OrderBy(q => q.String).ToList();
        }

        private List<Parameter> GetParameter(string parameterName)
        {
            if (Utils.doc == null)
            {
                return null;
            }

            if (Utils.selectedIds.Any())
            {
                var parameters = Utils.selectedIds.Select(w =>
                Utils.doc.GetElement(w).Parameters.
                    Cast<Parameter>().FirstOrDefault(q => q.Definition.Name == parameterName && IsParameterValid(q))).ToList();
                if (parameters.Any(q => q == null))
                {
                    Utils.Log($"Parameter {parameterName} does not exist for element ids {string.Join(",", Utils.selectedIds.Select(q => q.IntegerValue))}", Utils.LogLevel.Error);
                }
                return parameters;
            }
            else
            {
                var parameter = Utils.doc.ActiveView.Parameters.Cast<Parameter>()
                    .FirstOrDefault(q => q.Definition.Name == parameterName && IsParameterValid(q));
                if (parameter == null)
                {
                    Utils.Log($"Parameter {parameterName} does not exist in view {Utils.doc.ActiveView.Name}", Utils.LogLevel.Error);
                    return new List<Parameter>();
                }
                return new List<Parameter> { parameter };
            }
        }

        private bool IsParameterValid(Parameter p)
        {
            if (p.Definition is InternalDefinition id &&
                id.BuiltInParameter != BuiltInParameter.INVALID)
            {
                var typeid = id.GetParameterTypeId();
                if (typeid == ParameterTypeId.ScheduleLevelParam ||
                    typeid == ParameterTypeId.ScheduleBaseLevelParam ||
                    typeid == ParameterTypeId.ScheduleTopLevelParam)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        private string GetParameterValue(string parameterName)
        {
            if (Utils.doc == null)
            {
                return null;
            }

            var parameters = new List<Parameter>();
            if (Utils.selectedIds.Any())
            {
                foreach (var id in Utils.selectedIds)
                {
                    var element = Utils.doc.GetElement(id);
                    var parameter = Utils.GetParameter(element, parameterName);
                    if (parameter == null)
                        continue;
                    parameters.Add(parameter);
                }
            }
            else
            {
                var viewParam = Utils.GetParameter(Utils.doc.ActiveView, parameterName);
                if (viewParam != null)
                {
                    parameters.Add(viewParam);
                }
            }

            var values = new List<string>();
            foreach (var parameter in parameters)
            {
                if (parameter.StorageType == StorageType.ElementId)
                {
                    var id = parameter.AsElementId();
                    if (id == ElementId.InvalidElementId)
                    {
                        values.Add("None");
                    }
                    else
                    {
                        values.Add(Utils.doc.GetElement(id).Name);
                    }
                }
                else
                {
                    values.Add(parameter.AsValueString());
                }
            }
            var valuesDistinct = values.Distinct().ToList();
            if (valuesDistinct.Count == 1)
            {
                return valuesDistinct.First();
            }
            else
            {
                return string.Empty;
            }
        }

        private string AddSpacesToSentence(string text, bool preserveAcronyms)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            StringBuilder newText = new StringBuilder(text.Length * 2);
            newText.Append(text[0]);
            for (int i = 1; i < text.Length; i++)
            {
                if (char.IsUpper(text[i]))
                    if ((text[i - 1] != ' ' && !char.IsUpper(text[i - 1])) ||
                        (preserveAcronyms && char.IsUpper(text[i - 1]) &&
                         i < text.Length - 1 && !char.IsUpper(text[i + 1])))
                        newText.Append(' ');
                newText.Append(text[i]);
            }
            return newText.ToString();
        }
    }
}