using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RevitDataValidator
{
    public partial class FormEnterValue : Form
    {
        private string _pattern;

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