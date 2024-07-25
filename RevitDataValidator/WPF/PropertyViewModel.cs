using Autodesk.Revit.DB;
using Newtonsoft.Json;
using NLog;
using RevitDataValidator.Classes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace RevitDataValidator
{
    public class PropertyViewModel
    {
        private const string OTHER = "Others";
        public ObservableCollection<PackData> PackData { get; set; }
        public ObservableCollection<string> PackSets { get; set; }

        public ObservableCollection<WorksetRuleData> WorksetRuleDatas { get; set; }
        public ObservableCollection<ParameterRuleData> ParameterRuleDatas { get; set; }

        public PropertyViewModel()
        {
            SetRuleDatas();
            SetPackSets();
        }

        private Element SetPackSets()
        {
            Element element = null;

            if (Utils.selectedIds?.Count > 0)
            {
                element = Utils.doc.GetElement(Utils.selectedIds[0]);
            }
            else
            {
                element = Utils.doc?.ActiveView;
            }

            if (element == null || element.Category == null)
                return null;

            if (Utils.parameterUIData?.PackSets == null)
            {
                PackSets = new ObservableCollection<string>();
                PackData = new ObservableCollection<PackData>();
            }
            else
            {
                var catName = element.Category.Name;

                PackSets = new ObservableCollection<string>(
                    Utils.parameterUIData.PackSets
                    .Where(q => q.Category == catName).Select(q => q.Name));
            }
            return element;
        }

        public PropertyViewModel(string name)
        {
            SetRuleDatas();
            if (name == null || Utils.parameterUIData == null)
            {
                PackData = new ObservableCollection<PackData>();
                return;
            }
            Element element = SetPackSets();

            var packSet = Utils.parameterUIData.PackSets.Find(q => q.Name == name);
            if (packSet == null)
            {
                return;
            }
                
            if (packSet.ParameterPacks == null)
                packSet.ParameterPacks = new List<string>();

            var packNames = packSet.ParameterPacks;
            var allPacks = Utils.parameterUIData.ParameterPacks.Where(q => packNames?.Contains(q.Name) == true);
            var allParameterNames = allPacks.SelectMany(q => q.Parameters).ToList();

            var packOthers = new ParameterPack
            {
                Name = OTHER,
                Parameters = element.Parameters
                .Cast<Parameter>()
                .Where(q =>
                    !allParameterNames.Contains(q.Definition.Name) &&
                    (packSet.ShowAllOtherParametersExcluding?.Contains(q.Definition.Name) != true) &&
#if R2022 || R2023 || R2024
                    q.Definition.ParameterGroup != BuiltInParameterGroup.INVALID &&
#else
                    q.Definition.GetGroupTypeId() != new ForgeTypeId(string.Empty) &&
#endif
                    q.StorageType != StorageType.None)
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
                var parameterPack = Utils.parameterUIData.ParameterPacks.Find(q => q.Name == packName);
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
                        if (rule.Categories?.Contains(parameterPack.Category) == true &&
                            (rule.ListOptions != null || rule.KeyValues != null) &&
                            rule.ParameterName == pname)
                        {
                            List<StringInt> choices;
                            if (rule.ListOptions != null)
                            {
                                choices = rule.ListOptions.ConvertAll(q => new StringInt(q.Name, 0));
                            }
                            else
                            {
                                choices = rule.KeyValues.ConvertAll(q => new StringInt(q[0], 0));
                            }
                            var paramValue = GetParameterValue(pname);
                            var selected = choices.Find(q => q.String == paramValue);
                            if (parameters.Any(q => q != null))
                            {
                                packParameters.Add(new ChoiceStateParameter
                                {
                                    Parameters = parameters,
                                    Name = pname,
                                    Choices = choices,
                                    SelectedChoice = selected,
                                    IsEnabled = !parameters[0].IsReadOnly
                                });
                                foundRule = true;
                            }
                            break;
                        }
                    }
                    if (!foundRule)
                    {
                        if (parameters?.Count() > 0 && parameters[0] != null)
                        {
                            var parameter = parameters[0];
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
                                var typeid = parameter.GetTypeId();
                                var typeidstring = typeid.TypeId.Replace("autodesk.revit.parameter:", "");
                                typeidstring = typeidstring.Substring(0, typeidstring.IndexOf("-") + 1);
                                var path = Path.Combine(Utils.dllPath, "enums");
                                var enumFile = Directory.GetFiles(path, "*.json")
                                    .FirstOrDefault(q => q.Replace(path + "\\", "").StartsWith(typeidstring));
                                if (enumFile != null)
                                {
                                    using (var sr = new StreamReader(enumFile))
                                    {
                                        var contents = sr.ReadToEnd();
                                        var enumData = JsonConvert.DeserializeObject<ParameterEnum>(contents);
                                        choices = enumData.Properties.ConvertAll(q => new StringInt(q.Id, q.Value));
                                    }
                                }
                                else if (typeid == ParameterTypeId.WallKeyRefParam)
                                {
                                    foreach (var v in Enum.GetValues(typeof(WallLocationLine)))
                                    {
                                        choices.Add(new StringInt(AddSpacesToSentence(v.ToString(), true), (int)v));
                                    }
                                }

                                if (choices.Count != 0)
                                {
                                    var selected = choices.Find(q => q.Int == parameter.AsInteger());
                                    packParameters.Add(new ChoiceStateParameter
                                    {
                                        Parameters = parameters,
                                        Name = pname,
                                        IsEnabled = !parameters[0].IsReadOnly,
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
                                    if (choices.Count != 0)
                                    {
                                        try
                                        {
                                            StringInt selected = null;
                                            if (parameter.StorageType == StorageType.ElementId &&
                                                value != string.Empty)
                                            {
                                                selected = choices.Find(q => q.Int == parameter.AsElementId().IntegerValue);
                                            }
                                            else
                                            {
                                                selected = choices.Find(q => q.String == value);
                                            }
                                            packParameters.Add(new ChoiceStateParameter
                                            {
                                                Parameters = parameters,
                                                IsEnabled = !parameters[0].IsReadOnly,
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
                                        IsEnabled = !parameters[0].IsReadOnly,
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

        private void SetRuleDatas()
        {
            ParameterRuleDatas = new ObservableCollection<ParameterRuleData>();
            foreach (var rule in Utils.allParameterRules)
            {
                var v = new ParameterRuleData
                {
                    RuleName = rule.RuleName,
                    Guid = rule.Guid,
                };
                if (rule.ElementClasses != null)
                {
                    v.ParameterRuleCategories = new ObservableCollection<string>(
                        rule.ElementClasses.Select(q => q.Replace("Autodesk.Revit.DB.", "")));
                }
                else if (rule.Categories != null)
                {
                    v.ParameterRuleCategories = new ObservableCollection<string>(rule.Categories);
                }
                ParameterRuleDatas.Add(v);
            }

            WorksetRuleDatas = new ObservableCollection<WorksetRuleData>();
            foreach (var rule in Utils.allWorksetRules)
            {
                WorksetRuleDatas.Add(new WorksetRuleData
                {
                    WorksetName = rule.Workset,
                    WorksetRuleParameters = new ObservableCollection<ParameterData>(rule.Parameters),
                    WorksetRuleCategories = new ObservableCollection<string>(rule.Categories),
                    Guid = rule.Guid,
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

            if (Utils.selectedIds == null || Utils.selectedIds.Count == 0)
            {
                return new List<Parameter> { Utils.doc.ActiveView.Parameters.Cast<Parameter>()
                    .FirstOrDefault(q => q.Definition.Name == parameterName && IsParameterValid(q)) };
            }
            else
            {
                return Utils.selectedIds.ConvertAll(w =>
                Utils.doc.GetElement(w).Parameters.
                    Cast<Parameter>().FirstOrDefault(q => q.Definition.Name == parameterName && IsParameterValid(q)));
            }
        }

        private bool IsParameterValid(Parameter p)
        {
            if (p.Definition is InternalDefinition id &&
                id.BuiltInParameter != BuiltInParameter.INVALID)
            {
                var typeid = id.GetParameterTypeId();
                return typeid != ParameterTypeId.ScheduleLevelParam &&
                    typeid != ParameterTypeId.ScheduleBaseLevelParam &&
                    typeid != ParameterTypeId.ScheduleTopLevelParam;
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
            if (Utils.selectedIds == null || Utils.selectedIds.Count == 0)
            {
                var viewParam = Utils.GetParameter(Utils.doc.ActiveView, parameterName);
                if (viewParam != null)
                {
                    parameters.Add(viewParam);
                }
            }
            else
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
                return valuesDistinct[0];
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
                {
                    if ((text[i - 1] != ' ' && !char.IsUpper(text[i - 1])) ||
                        (preserveAcronyms && char.IsUpper(text[i - 1]) &&
                         i < text.Length - 1 && !char.IsUpper(text[i + 1])))
                    {
                        _ = newText.Append(' ');
                    }
                }

                newText.Append(text[i]);
            }
            return newText.ToString();
        }
    }
}