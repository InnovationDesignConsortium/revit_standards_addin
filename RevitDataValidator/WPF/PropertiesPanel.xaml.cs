using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitDataValidator.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            data.FrameworkElement = this as FrameworkElement;
            var state = new DockablePaneState
            {
                DockPosition = DockPosition.Floating
            };
            var height = 800;
            var top = 20;
            var left = 0;
            var width = 550;
            state.SetFloatingRectangle(new Autodesk.Revit.DB.Rectangle
            {
                Top = top,
                Bottom = top + height,
                Left = left,
                Right = left + width
            });
            data.InitialState = state;
        }

        private void cbo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbo.SelectedItem == null)
                return;

            DataContext = new PropertyViewModel(cbo.SelectedItem.ToString());

            var element = Utils.doc.GetElement(Utils.selectedIds.First());
            if (element.Category == null)
                return;

            var catName = element.Category.Name;

            if (Utils.dictCategoryPackSet.ContainsKey(catName))
                Utils.dictCategoryPackSet[catName] = Utils.propertiesPanel.cbo.SelectedItem.ToString();
            else
                Utils.dictCategoryPackSet.Add(catName, Utils.propertiesPanel.cbo.SelectedItem.ToString());
        }

        public void Refresh(string packSetName)
        {
            DataContext = new PropertyViewModel(packSetName);
        }

        public void Refresh()
        {
            if (cbo.SelectedItem == null)
                return;

            DataContext = new PropertyViewModel(cbo.SelectedItem.ToString());
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

        private void SaveTextBoxValue(System.Windows.Controls.TextBox textBox)
        {
            if (textBox.Tag is List<Parameter> parameters)
            {
                var parameter = parameters.First();
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
                Utils.eventHandlerWithParameterObject.Raise(item);
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

        private void SetCheckboxValue(CheckBox control)
        {
            if (control.Tag is List<Parameter> parameters)
            {
                var item = new ParameterObject(parameters, control.IsChecked.ToString());
                Utils.eventHandlerWithParameterObject.Raise(item);
            }
        }

        private void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var control = sender as System.Windows.Controls.ComboBox;
            if (control.IsDropDownOpen)
                return;
            if (control.Tag is List<Parameter> parameters)
            {
                ParameterObject po = null;
                if (control.SelectedItem is StringInt stringInt)
                {
                    if (stringInt.Int == 0)
                    {
                        po = new ParameterObject(parameters, stringInt.String);
                    }
                    else
                    {
                        po = new ParameterObject(parameters, stringInt.Int);
                    }
                }
                Utils.eventHandlerWithParameterObject.Raise(po);
            }
        }


        private void TextBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            System.Windows.Controls.TextBox tb = (System.Windows.Controls.TextBox)sender;
            tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()));
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Hyperlink_RequestNavigate_Pdf(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
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
                        .Select(q => new StringInt(q.Family.FamilyCategory.Name + "-" + q.FamilyName + "-" + q.Name, q.Id.IntegerValue)).ToList();

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
    }
}