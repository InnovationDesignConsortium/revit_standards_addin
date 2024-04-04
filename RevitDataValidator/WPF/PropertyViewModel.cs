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
            var element = Utils.doc.GetElement(Utils.selectedIds.First());
            if (element.Category == null)
                return;

            var catName = element.Category.Name;

            cboData = new ObservableCollection<string>(
                Utils.parameterUIData.PackSets
                .Where(q => q.Category == catName).Select(q => q.Name));

            var packSet = Utils.parameterUIData.PackSets.FirstOrDefault(q => q.Name == name);
            var packNames = packSet.ParameterPacks;
            var allPacks = Utils.parameterUIData.ParameterPacks.Where(q => packNames.Contains(q.Name));
            var allParameterNames = allPacks.SelectMany(q => q.Parameters).ToList();

            var packOthers = new ParameterPack
            {
                Name = OTHER,
                Parameters = element.Parameters
                .Cast<Parameter>()
                .Where(q => !allParameterNames.Contains(q.Definition.Name))
                .Where(q => packSet.ShowAllOtherParametersExcluding == null || !packSet.ShowAllOtherParametersExcluding.Contains(q.Definition.Name))
                .Where(q => q.Definition.ParameterGroup != BuiltInParameterGroup.INVALID)
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
                    continue;

                var packParameters = new ObservableCollection<IStateParameter>();
                foreach (string pname in parameterPack.Parameters)
                {
                    var parameters = GetParameter(pname);
                    bool foundRule = false;
                    foreach (var rule in Utils.allRules)
                    {
                        if (rule.Categories.Contains(parameterPack.Category) &&
                            rule.ListOptions != null &&
                            rule.ParameterName == pname)
                        {
                            var choices = rule.ListOptions.Select(q => new StringInt(q.Name, 0)).ToList();
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
                                if (pname == "Location Line")
                                {
                                    foreach (var v in Enum.GetValues(typeof(WallLocationLine)))
                                    {
                                        choices.Add(new StringInt(AddSpacesToSentence(v.ToString(), true), (int)v));
                                    }
                                }
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
                            else if (parameter.StorageType == StorageType.ElementId)
                            {
                                if (parameter.Definition is InternalDefinition id)
                                {
                                    var typeid = id.GetParameterTypeId();
                                    List<StringInt> choices = new List<StringInt>();
                                    if (typeid == ParameterTypeId.PhaseCreated ||
                                        typeid == ParameterTypeId.PhaseDemolished)
                                    {
                                        choices = new FilteredElementCollector(Utils.doc)
                                            .OfClass(typeof(Phase))
                                            .Select(q => new StringInt(q.Name, q.Id.IntegerValue))
                                            .OrderBy(q => q.String).ToList();

                                        if (typeid == ParameterTypeId.PhaseDemolished)
                                        {
                                            choices.Add(new StringInt("None", -1));
                                        }
                                    }
                                    else if (typeid == ParameterTypeId.WallBaseConstraint ||
                                        typeid == ParameterTypeId.WallHeightType ||
                                        typeid == ParameterTypeId.RoofBaseLevelParam ||
                                        typeid == ParameterTypeId.RoofUptoLevelParam ||
                                        typeid == ParameterTypeId.FamilyBaseLevelParam ||
                                        typeid == ParameterTypeId.FamilyTopLevelParam ||
                                        typeid == ParameterTypeId.FamilyLevelParam ||
                                        typeid == ParameterTypeId.StairsBaseLevelParam ||
                                        typeid == ParameterTypeId.StairsTopLevelParam)
                                    {
                                        choices = new FilteredElementCollector(Utils.doc)
                                            .OfClass(typeof(Level))
                                            .Select(q => new StringInt(q.Name, q.Id.IntegerValue))
                                            .OrderBy(q => q.String).ToList();

                                        if (typeid == ParameterTypeId.StairsTopLevelParam ||
                                            typeid == ParameterTypeId.RoofUptoLevelParam)
                                        {
                                            choices.Add(new StringInt("None", -1));
                                        }
                                        else if (typeid == ParameterTypeId.WallHeightType)
                                        {
                                            choices.Add(new StringInt("Unconnected", -1));
                                        }
                                    }
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
                                    }
                                }
                            }
                            else
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

        private List<Parameter> GetParameter(string parameterName)
        {
            if (Utils.doc == null)
            {
                return null;
            }

            return Utils.selectedIds.Select(w =>
            Utils.doc.GetElement(w).Parameters.
                Cast<Parameter>().FirstOrDefault(q => q.Definition.Name == parameterName && IsParameterValid(q))).ToList();
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
            foreach (var id in Utils.selectedIds)
            {
                var element = Utils.doc.GetElement(id);
                var parameter = element.LookupParameter(parameterName);
                if (parameter == null)
                    continue;
                parameters.Add(parameter);
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

        string AddSpacesToSentence(string text, bool preserveAcronyms)
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