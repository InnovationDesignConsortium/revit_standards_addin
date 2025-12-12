using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
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
                    .Where(q => q.Category.Contains(catName) || q.Category.Contains(Utils.ALL)).Select(q => q.Name));
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
            if (element == null)
            {
                return;
            }

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
#if R2022 || R2023
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

            var parameterValues = new Dictionary<string, List<string>>();
            var selectedElements = new UIDocument(Utils.doc).Selection.GetElementIds().Select(q => Utils.doc.GetElement(q)).ToList();

            if (!selectedElements.Any())
            {
                selectedElements.Add(Utils.doc.ActiveView);
            }

            // get all parameters in all packs for this set
            var allParameterNamesInThisPackSet = new List<string>();
            foreach (var packName in packNames)
            {
                var parameterPack = Utils.parameterUIData.ParameterPacks.Find(q => q.Name == packName);
                if (packName == OTHER)
                {
                    parameterPack = packOthers;
                }
                allParameterNamesInThisPackSet.AddRange(parameterPack.Parameters);
            }

            var dictParameterInfo = new Dictionary<string, ParameterInfo>();

            foreach (var selectedElement in selectedElements)
            {
                var allParams = new List<Parameter>();
                var selectedElementParameters = selectedElement.Parameters.Cast<Parameter>()
                    .Where(q => allParameterNamesInThisPackSet.Contains(q.Definition.Name) && Utils.IsParameterValid(q));
                var selectedTypeParameters = Utils.doc.GetElement(selectedElement.GetTypeId())?.Parameters.Cast<Parameter>().
                    Where(q => allParameterNamesInThisPackSet.Contains(q.Definition.Name) && Utils.IsParameterValid(q));
                allParams.AddRange(selectedElementParameters);
                allParams.AddRange(selectedTypeParameters);
                foreach (var param in allParams.Where(q => q != null))
                {
                    if (dictParameterInfo.ContainsKey(param.Definition.Name))
                    {
                        var pi = dictParameterInfo[param.Definition.Name];
                        pi.Values.Add(param.AsValueString());
                        pi.Parameters.Add(param);
                    }
                    else
                    {
                        var p = new ParameterInfo
                        {
                            StorageType = param.StorageType,
                            Values = new List<string> { param.AsValueString() },
                            Parameters = new List<Parameter> { param }
                        };
                        dictParameterInfo.Add(param.Definition.Name, p);
                    }
                }
            }

            foreach (var packName in packNames)
            {
                var parameterPack = Utils.parameterUIData.ParameterPacks.Find(q => q.Name == packName);
                if (packName == OTHER)
                {
                    parameterPack = packOthers;
                }

                if (parameterPack == null)
                {
                    Utils.Log($"Parameter Pack '{packName}' is listed in the Pack Set '{packSet.Name}' but does not exist", LogLevel.Error);
                    continue;
                }

                var packParameters = new ObservableCollection<IStateParameter>();
                foreach (string pname in parameterPack.Parameters)
                {
                    if (!dictParameterInfo.TryGetValue(pname, out ParameterInfo pinfo))
                    {
                        continue;
                    }
                    if (pinfo == null)
                    {
                        continue;
                    }
                    var parameters = pinfo.Parameters;
                    bool foundRule = false;
                    if (pinfo.Values.Any())
                    {
                        foreach (var rule in Utils.allParameterRules.Where(q => !q.Disabled))
                        {
                            if (
                                rule.Categories != null &&
                                (rule.Categories.Contains(element.Category.Name) || rule.Categories.Contains(Utils.ALL)) &&
                                (rule.ListOptions != null || rule.KeyValues != null || rule.DictKeyValues != null) &&
                                rule.ParameterName == pname)
                            {
                                List<StringInt> choices;
                                if (rule.ListOptions != null)
                                {
                                    choices = Utils.GetChoicesFromList(element, rule)
                                        .ConvertAll(q => new StringInt(q.Name, 0));
                                    if (choices.Count == 0 || !choices.Select(q => q.String).Contains(pinfo.Values.First()))
                                    {
                                        foreach (var parameter in pinfo.Parameters.Where(q => q != null))
                                        {
                                            if (parameter.AsValueString() != "")
                                            {
                                                Utils.eventHandlerWithParameterObject.Raise(new List<ParameterObject>
                                    {
                                        new ParameterObject(parameters, "")
                                    });
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    var keyValues = rule.KeyValues;
                                    if (rule.FilterParameter != null)
                                    {
                                        keyValues = Utils.GetKeyValuesFromFilterParameter(rule);
                                    }
                                    choices = keyValues.ConvertAll(q => new StringInt(q[0], 0));
                                }
                                var paramValue = GetParameterValue(pinfo.Values);
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
                    }
                    if (!foundRule)
                    {
                        if (parameters?.Count > 0 && parameters[0] != null)
                        {
                            var parameter = parameters[0];
                            var isElementType = parameter.Element is ElementType;
                            var value = GetParameterValue(pinfo.Values);
                            if (parameter.StorageType == StorageType.Integer &&
                                parameter.Definition.GetDataType() == SpecTypeId.Boolean.YesNo)
                            {
                                var boolValue = false;
                                if (value == "Yes")
                                    boolValue = true;

                                var boolParam = new BoolStateParameter
                                {
                                    Name = pname,
                                    Parameters = parameters,
                                    IsEnabled = !parameters.First().IsReadOnly && !isElementType,
                                    Value = boolValue
                                };
                                if (isElementType)
                                {
                                    boolParam.ToolTipText = "Type Parameters are read only";
                                }
                                packParameters.Add(boolParam);
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
                                    using (var sr = new StreamReader(new FileStream(enumFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                                    {
                                        var contents = sr.ReadToEnd();
                                        var enumData = JsonConvert.DeserializeObject<ParameterEnum>(contents, new JsonSerializerSettings
                                        {
                                            Error = Utils.HandleDeserializationError,
                                            MissingMemberHandling = MissingMemberHandling.Ignore
                                        });
                                        if (enumData.Properties != null)
                                        { 
                                            choices = enumData.Properties.ConvertAll(q => new StringInt(q.Id, q.Value));
                                        }
                                        else if (enumData.@enum != null)
                                        {
                                            choices = enumData.@enum.ConvertAll(q => new StringInt(q.id, q.value));
                                        }
                                    }
                                }
                                else if (typeid == ParameterTypeId.WallKeyRefParam)
                                {
                                    foreach (var v in System.Enum.GetValues(typeof(WallLocationLine)))
                                    {
                                        choices.Add(new StringInt(AddSpacesToSentence(v.ToString(), true), (int)v));
                                    }
                                }
                                else if (pname == "Workset")
                                {
                                    foreach (var workset in new FilteredWorksetCollector(Utils.doc)
                                        .Where(q => q.Kind == WorksetKind.UserWorkset).OrderBy(q => q.Name))
                                    {
                                        choices.Add(new StringInt(workset.Name, workset.Id.IntegerValue));
                                    }
                                }

                                if (choices.Count != 0)
                                {
                                    var selected = choices.Find(q => q.Long == parameter.AsInteger());
                                    var choiceParam = new ChoiceStateParameter
                                    {
                                        Parameters = parameters,
                                        Name = pname,
                                        IsEnabled = !parameters[0].IsReadOnly && !isElementType,
                                        Choices = choices,
                                        SelectedChoice = selected
                                    };
                                    if (isElementType)
                                    {
                                        choiceParam.ToolTipText = "Type Parameters are read only";
                                    }
                                    packParameters.Add(choiceParam);
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
                                    var choices = new List<StringInt>();
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
                                                    .Select(q => new StringInt(q.Name, ElementIdExtension.GetValue(q.Id)))
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
                                                .Select(q => new StringInt(q.Name, ElementIdExtension.GetValue(q.Id)))
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
                                        else if (typeid == ParameterTypeId.ElemTypeParam)
                                        {
                                            choices.Add(new StringInt(parameter.AsValueString(), ElementIdExtension.GetValue(parameter.AsElementId())));
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
                                                selected = choices.Find(q => q.Long == ElementIdExtension.GetValue(parameter.AsElementId()));
                                            }
                                            else
                                            {
                                                selected = choices.Find(q => q.String == value);
                                            }
                                            var choiceParam = new ChoiceStateParameter
                                            {
                                                Parameters = parameters,
                                                IsEnabled = !parameters[0].IsReadOnly && !isElementType,
                                                Name = pname,
                                                Choices = choices,
                                                SelectedChoice = selected
                                            };
                                            if (isElementType)
                                            {
                                                choiceParam.ToolTipText = "Type Parameters are read only";
                                            }
                                            packParameters.Add(choiceParam);
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
                                var textParameter = new TextStateParameter
                                {
                                    Name = pname,
                                    Parameters = parameters,
                                    IsEnabled = !parameters[0].IsReadOnly && !isElementType,
                                    Value = value
                                };
                                if (isElementType)
                                {
                                    textParameter.ToolTipText = "Type Parameters are read only";
                                }
                                packParameters.Add(textParameter);
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
            Utils.currentPropertyViewModelName = name;
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
                        .Select(q => new StringInt(q.Name, ElementIdExtension.GetValue(q.Id)))
                        .OrderBy(q => q.String).ToList();
        }

        private List<StringInt> GetChoices(BuiltInCategory bic)
        {
            return new FilteredElementCollector(Utils.doc)
                        .OfCategory(bic)
                        .Select(q => new StringInt(q.Name, ElementIdExtension.GetValue(q.Id)))
                        .OrderBy(q => q.String).ToList();
        }

        private string GetParameterValue(List<string> values)
        {
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