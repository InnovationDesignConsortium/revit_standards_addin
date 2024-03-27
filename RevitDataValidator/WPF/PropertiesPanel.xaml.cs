using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows;
using System.Windows.Controls;

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
            DataContext = new PropertyViewModel(cbo.SelectedItem.ToString());
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var control = sender as System.Windows.Controls.TextBox;
            var param = control.Tag as Parameter;
            if (param.StorageType == StorageType.Double)
            {
                var units = Utils.doc.GetUnits();
                var specTypeId = param.Definition.GetDataType();

                if (UnitFormatUtils.TryParse(units, specTypeId, control.Text, out double d))
                {
                    var parsed = UnitFormatUtils.Format(units, param.Definition.GetDataType(), d, true);
                    control.Text = parsed;
                }
            }
            
            var item = new ParameterValue(param, control.Text);
            Utils.eventHandlerWithParameterValue.Raise(item);
        }

        private void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var control = sender as System.Windows.Controls.ComboBox;
            if (control.IsDropDownOpen)
                return;

            var item = new Property(control.Tag.ToString(), control.Text);
            Utils.eventHandlerWithProperty.Raise(item);
        }

        private void TextBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            System.Windows.Controls.TextBox tb = (System.Windows.Controls.TextBox)sender;
            tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()));
        }
    }
}