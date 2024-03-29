using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RevitDataValidator
{
    public partial class FormEnterValue : Form
    {
        string _pattern;
        public FormEnterValue(string message, string regexpattern)
        {
            InitializeComponent();
            label1.Text = message;
            _pattern = regexpattern;
        }

        public string GetValue()
        {
            return textBox1.Text;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (_pattern == null || Regex.IsMatch(textBox1.Text, _pattern))
            {
                Close();
            }
        }
    }
}
