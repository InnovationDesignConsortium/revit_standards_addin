using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            cboData = new ObservableCollection<string>(Utils.parameterUIData.PackSets.Select(q => q.Name));

            var packs = Utils.parameterUIData.PackSets.FirstOrDefault(q => q.Name == name).ParameterPacks;

            PackData = new ObservableCollection<PackData>();

            foreach (var parameterPack in Utils.parameterUIData.ParameterPacks
                .Where(q => packs.Contains(q.Name)))
            {
                var stateParametersList = new ObservableCollection<IStateParameter>();
                foreach (string pname in parameterPack.Parameters)
                {
                    bool foundRule = false;
                    foreach (var rule in Utils.allRules)
                    {
                        if (rule.Categories.Contains(parameterPack.Category) &&
                            rule.ParameterName == pname)
                        {
                            stateParametersList.Add(new ChoiceStateParameter
                            {
                                Name = pname,
                                Choices = rule.ListOptions.Select(q => q.Name).ToList(),
                                SelectedChoice = GetParameterValue(pname)
                            });
                            foundRule = true;
                            break;
                        }
                    }
                    if (!foundRule)
                    {
                        stateParametersList.Add(
                            new TextStateParameter
                            {
                                Name = pname,
                                Value = GetParameterValue(pname)
                            }
                            );
                    }
                }
                PackData.Add(new PackData
                {
                    ParameterName = parameterPack.Name,
                    StateParametersList = stateParametersList
                });
            }
        }

        private string GetParameterValue(string parameterName)
        {
            if (Utils.doc == null)
            {
                return null;
            }

            var list = new List<Parameter>();
            foreach (var id in Utils.selectedIds)
            {
                var element = Utils.doc.GetElement(id);
                var parameter = element.LookupParameter(parameterName);
                if (parameter == null)
                    continue;
                list.Add(parameter);
            }

            var distinct = list.Distinct();
            if (distinct.Count() == 1)
            {
                return list.First().AsValueString();
            }
            else
            {
                return string.Empty;
            }
        }
    }
}