using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace RevitDataValidator.Forms
{
    public partial class FormEnableDisabledRules : Form
    {
        public FormEnableDisabledRules()
        {
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            var rules = new List<BaseRule>();
            rules.AddRange(Utils.allParameterRules);
            rules.AddRange(Utils.allWorksetRules);

            for (int i = 0; i < lstRules.Items.Count; i++)
            {
                var item = lstRules.Items[i];
                var rule = rules.FirstOrDefault(q => q.RuleName == item.ToString());
                if (rule != null)
                {
                    rule.Disabled = !lstRules.GetItemChecked(i);
                }
            }

            Close();
        }

        private void FormEnableDisabledRules_Load(object sender, EventArgs e)
        {
            var rules = new List<BaseRule>();
            rules.AddRange(Utils.allParameterRules);
            rules.AddRange(Utils.allWorksetRules);
            foreach (var rule in rules.Where(q => q.DisableByDefault))
            {
                var i = lstRules.Items.Add(rule.RuleName);
                if (!rule.Disabled)
                {
                    lstRules.SetItemChecked(i, true);
                }
            }
        }
    }
}