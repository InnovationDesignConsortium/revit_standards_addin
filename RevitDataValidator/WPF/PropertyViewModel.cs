using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

namespace RevitDataValidator
{
    public class PropertyViewModel
    {
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

            var packs = Utils.parameterUIData.PackSets.FirstOrDefault(q => q.Name == name).ParameterPacks;

            PackData = new ObservableCollection<PackData>();

            foreach (var packName in packs)
            {
                var parameterPack = Utils.parameterUIData.ParameterPacks.FirstOrDefault(q => q.Name == packName);
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
                                SelectedChoice = selected
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
                                       Value = boolValue
                                   }
                                   );
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
                                        Value = value
                                    }
                                    );
                            }
                        }
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
            if (p.Definition is InternalDefinition id)
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
    }
}