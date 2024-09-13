using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.Async;
using RevitDataValidator.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RevitDataValidator
{
    public partial class PropertiesPanel : IDockablePaneProvider
    {
        public PropertiesPanel()
        {
            InitializeComponent();
            DataContext = new PropertyViewModel();
        }

        void IDockablePaneProvider.SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = this;
            var state = new DockablePaneState
            {
                DockPosition = DockPosition.Left
            };
            data.InitialState = state;
        }

        private void cboParameterPack_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboParameterPack == null || cboParameterPack.SelectedItem == null)
                return;

            var filename = Utils.GetFileName();
            if (Utils.dictFileActivePackSet.ContainsKey(filename))
            {
                Utils.dictFileActivePackSet[filename] = cboParameterPack.SelectedItem.ToString();
            }
            else
            {
                Utils.dictFileActivePackSet.Add(filename, cboParameterPack.SelectedItem.ToString());
            }

            if (cboParameterPack.SelectedItem == null)
            {
                DataContext = new PropertyViewModel();
            }
            else
            {
                DataContext = new PropertyViewModel(cboParameterPack.SelectedItem.ToString());
            }

            Element element;
            if (Utils.selectedIds == null || Utils.selectedIds.Count == 0)
            {
                element = Utils.doc.ActiveView;
            }
            else
            {
                element = Utils.doc.GetElement(Utils.selectedIds[0]);
            }

            if (element.Category == null)
                return;

            var catName = element.Category.Name;

            Utils.dictCategoryPackSet[catName] = Utils.propertiesPanel.cboParameterPack.SelectedItem.ToString();
        }

        public void Refresh(string packSetName)
        {
            DataContext = new PropertyViewModel(packSetName);
        }

        public void Refresh()
        {
            if (cboParameterPack.SelectedItem == null || Utils.parameterUIData?.PackSets == null)
            {
                DataContext = new PropertyViewModel();
            }
            else
            {
                DataContext = new PropertyViewModel(cboParameterPack.SelectedItem.ToString());
            }
        }

        public void SaveTextBoxValues()
        {
            foreach (System.Windows.Controls.TextBox textBox in FindVisualChildren<System.Windows.Controls.TextBox>(this))
            {
                SaveTextBoxValue(textBox);
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveTextBoxValue(sender as System.Windows.Controls.TextBox);
        }

        private static void SaveTextBoxValue(System.Windows.Controls.TextBox textBox)
        {
            if (textBox.Tag is List<Parameter> parameters)
            {
                var parameter = parameters[0];
                if (parameter.StorageType == StorageType.Double)
                {
                    var units = Utils.doc.GetUnits();
                    var specTypeId = parameter.Definition.GetDataType();

                    if (UnitFormatUtils.TryParse(units, specTypeId, textBox.Text, out double d))
                    {
                        var parsed = UnitFormatUtils.Format(units, parameter.Definition.GetDataType(), d, true);
                        textBox.Text = parsed;
                    }
                }
                var item = new ParameterObject(parameters, textBox.Text);
                Utils.eventHandlerWithParameterObject.Raise(new List<ParameterObject> { item });
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SetCheckboxValue(sender as CheckBox);
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetCheckboxValue(sender as CheckBox);
        }

        private static void SetCheckboxValue(CheckBox control)
        {
            if (control.Tag is List<Parameter> parameters)
            {
                var item = new ParameterObject(parameters, control.IsChecked.ToString());
                Utils.eventHandlerWithParameterObject.Raise(new List<ParameterObject> { item });
            }
        }

        private void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var control = sender as System.Windows.Controls.ComboBox;
            if (control.IsDropDownOpen)
                return;
            if (control.Tag is List<Parameter> parameters)
            {
                if (control.SelectedItem is StringInt stringInt)
                {
                    Utils.eventHandlerWithParameterObject.Raise(new List<ParameterObject> {
                        new ParameterObject(parameters, stringInt) });
                }
            }
        }

        private void TextBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            System.Windows.Controls.TextBox tb = (System.Windows.Controls.TextBox)sender;
            tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()));
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Utils.StartShell(e.Uri.AbsoluteUri, false);
        }

        private void Hyperlink_RequestNavigate_Pdf(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Utils.StartShell(e.Uri.AbsoluteUri, false);
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield return (T)Enumerable.Empty<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject ithChild = VisualTreeHelper.GetChild(depObj, i);
                if (ithChild == null) continue;
                if (ithChild is T t) yield return t;
                foreach (T childOfChild in FindVisualChildren<T>(ithChild)) yield return childOfChild;
            }
        }

        private void Button_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Content.ToString() == "Place Furniture Instance In Room")
                {
                    List<StringInt> elements = new FilteredElementCollector(Utils.doc)
                        .OfClass(typeof(FamilySymbol))
                        .WherePasses(new ElementMulticategoryFilter(
                            new List<BuiltInCategory> {
                                BuiltInCategory.OST_Furniture,
                                BuiltInCategory.OST_SpecialityEquipment
                            }))
                        .Cast<FamilySymbol>()
                        .OrderBy(q => q.Category.Name)
                        .ThenBy(q => q.FamilyName)
                        .ThenBy(q => q.Name)
                        .Select(q => new StringInt(q.Family.FamilyCategory.Name + "-" + q.FamilyName + "-" + q.Name, ElementIdExtension.GetValue(q.Id))).ToList();

                    using (FormSelectElements form = new FormSelectElements(elements))
                    {
                        if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            var ids = form.getIds();
                            Utils.eventHandlerCreateInstancesInRoom.Raise(ids);
                        }
                    }
                }
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void Button_Workset_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var tag = button.Tag;
            var rule = Utils.allWorksetRules.First(q => q.Guid.ToString() == tag.ToString());
            if (rule == null)
                return;

            List<ElementId> ids = new FilteredElementCollector(Utils.doc)
                .WherePasses(new LogicalOrFilter(
                    new ElementIsElementTypeFilter(true),
                    new ElementIsElementTypeFilter(false))).ToElementIds().ToList();

            Utils.RunWorksetRule(rule, ids);
        }

        private async void Button_Parameter_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var tag = button.Tag;
            var rule = Utils.allParameterRules.First(q => q.Guid.ToString() == tag.ToString());
            if (rule == null)
                return;

            var ruleFailures = new List<RuleFailure>();
            var parametersToSet = new List<ParameterString>();

            if (rule.CustomCode != null && Utils.dictCustomCode.ContainsKey(rule.CustomCode))
            {
                var ids = await RevitTask.RaiseGlobal<CustomRuleExternalEventHandler, ParameterRule, IEnumerable<ElementId>>(rule);
                
                if (ids != null && ids.Any())
                {
                    var td = new TaskDialog("Error")
                    {
                        MainInstruction = rule.UserMessage,
                        MainContent = string.Join(Environment.NewLine, ids)
                    };
                    td.Show();
                }
            }
            else
            {
                var filteredElementCollector = new FilteredElementCollector(Utils.doc)
                    .WherePasses(new LogicalOrFilter(
                        new ElementIsElementTypeFilter(true),
                        new ElementIsElementTypeFilter(false)));

                if (rule.Categories != null)
                {
                    var cats = new List<BuiltInCategory>();
                    var allBic = Enum.GetValues<BuiltInCategory>().Cast<BuiltInCategory>();
                    foreach (var cat in rule.Categories)
                    {
                        var thisbic = Utils.catMap[cat];
                        cats.Add(thisbic);
                    }
                    filteredElementCollector = filteredElementCollector.WherePasses(new ElementMulticategoryFilter(cats));
                }
                if (rule.ElementClasses != null)
                {
                    var types = new List<Type>();
                    foreach (var className in rule.ElementClasses)
                    {
                        var type = Type.GetType(className);
                        types.Add(type);
                    }
                    filteredElementCollector = filteredElementCollector.WherePasses(new ElementMulticlassFilter(types));
                }

                var ids = filteredElementCollector.ToElementIds();
                var parametersToSetForFormatRules = new List<ParameterString>();

                foreach (var id in ids)
                {
                    var thisFailure = Utils.RunParameterRule(
                        rule,
                        id,
                        null,
                        out List<ParameterString> thisElementParametersToSet,
                        out List<ParameterString> thisElementparametersToSetForFormatRules
                        );
                    parametersToSet.AddRange(thisElementParametersToSet);
                    parametersToSetForFormatRules.AddRange(thisElementparametersToSetForFormatRules);
                    if (thisFailure != null)
                    {
                        ruleFailures.Add(thisFailure);
                    }
                }

                if (parametersToSetForFormatRules.Count != 0)
                {
                    var td = Utils.GetTaskDialogForFormatRenaming(rule, parametersToSetForFormatRules);
                    if (td.Show() == TaskDialogResult.Ok)
                    {
                        Utils.eventHandlerWithParameterObject.Raise(
                            parametersToSetForFormatRules.ConvertAll(q => new ParameterObject(new List<Parameter> { q.Parameter }, q.NewValue)));
                    }
                }

                if (parametersToSet.Count != 0)
                {
                    Utils.eventHandlerWithParameterObject.Raise(
                        parametersToSet.ConvertAll(q => new ParameterObject(new List<Parameter> { q.Parameter }, q.NewValue)));
                }

                if (ruleFailures.Count != 0)
                {
                    FormGridList form = new FormGridList(ruleFailures);
                    form.Show();
                }
            }
        }

        private void Button_ViewRuleFile_Click(object sender, RoutedEventArgs e)
        {
            var filename = Utils.GetFileName();
            var ruleFileInfo = Utils.ruleDatas;
            string fileToOpen = null;
            if (ruleFileInfo.TryGetValue(filename, out var ruleFile))
            {
                if (ruleFile == null)
                {
                    return;
                }
                if (!string.IsNullOrEmpty(ruleFile.Url))
                {
                    fileToOpen = ruleFile.Url;
                }
                else if (!string.IsNullOrEmpty(ruleFile.Filename))
                {
                    fileToOpen = ruleFile.Filename;
                }
            }
            if (fileToOpen != null)
            {
                Utils.StartShell(fileToOpen, true);
            }
        }
    }
}