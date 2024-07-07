using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace RevitDataValidator.Forms
{
    public partial class FormSelectElements : System.Windows.Forms.Form
    {
        public FormSelectElements(List<StringInt> stringInts)
        {
            InitializeComponent();
            listBox1.DataSource = stringInts;
            listBox1.DisplayMember = "String";
            listBox1.ValueMember = "Int";
            if (listBox1.Items.Count > 0)
                listBox1.SelectedIndex = 0;
        }

        public List<ElementId> getIds()
        {
            List<ElementId> ids = new List<ElementId>();
            foreach (var obj in listBox1.SelectedItems.Cast<StringInt>())
            {
                ids.Add(new ElementId(obj.Int));
            }
            return ids;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItems.Count == 0)
            {
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}