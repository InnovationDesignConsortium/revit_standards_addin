using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RevitDataValidator
{
    public partial class FormSelectFromList : Form
    {
        public FormSelectFromList(List<string> values, string message)
        {
            InitializeComponent();
            listBox1.Items.Clear();
            foreach (var v in values)
            {
                listBox1.Items.Add(v);
            }
            label1.Text = message;
        }

        public string GetValue()
        {
            return listBox1.SelectedItem.ToString();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItems.Count == 0)
            {
                return;
            }
            Close();
        }
    }
}
