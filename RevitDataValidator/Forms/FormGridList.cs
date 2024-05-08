using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;

namespace RevitDataValidator.Forms
{
    public partial class FormGridList : System.Windows.Forms.Form
    {
        const string PARAM = "PARAM";
        public FormGridList(List<RuleFailure> failures)
        {
            InitializeComponent();

            try
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("Category");
                dt.Columns.Add("Family");
                dt.Columns.Add("Name");
                dt.Columns.Add("Parameter");
                dt.Columns.Add("Id");
                dt.Columns.Add("Message");

                var rulesByParameterName = failures.GroupBy(q => q.Rule.ParameterName);
                foreach (var group in rulesByParameterName)
                {
                    dt.Columns.Add(group.Key);
                }

                bool hasFamily = false;
                var failuresOnePerElement = failures
                  .GroupBy(p => p.ElementId)
                  .Select(g => g.First())
                  .ToList();

                foreach (RuleFailure ruleFailure in failuresOnePerElement)
                {
                    var dr = dt.NewRow();
                    var element = Utils.doc.GetElement(ruleFailure.ElementId);
                    dr["Category"] = element.Category.Name;
                    dr["Name"] = element.Name;
                    dr["Id"] = element.Id.IntegerValue.ToString();
                    if (element is FamilyInstance fi)
                    {
                        dr["Family"] = fi.Symbol.Family.Name;
                        hasFamily = true;
                    }
                    foreach (var group in rulesByParameterName)
                    {
                        dr[group.Key] = Utils.GetParameter(element, group.Key).AsValueString();
                    }

                    dt.Rows.Add(dr);
                }
                dataGridView1.AutoGenerateColumns = false;

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Id",
                    HeaderText = "Id",
                    Name = "Id",
                    ReadOnly = true,
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Category",
                    HeaderText = "Category",
                    ReadOnly = true,
                });

                if (hasFamily)
                {
                    dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        DataPropertyName = "Family",
                        HeaderText = "Family",
                        ReadOnly = true,
                    });
                }

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Name",
                    HeaderText = "Name",
                    ReadOnly = true,
                });

                var labelY = 10;
                const int multiUiX = 250;
                foreach (var group in rulesByParameterName)
                {
                    labelY += 35;
                    var rule = group.First().Rule;
                    var parameterMultiLabel = new System.Windows.Forms.Label
                    {
                        Text = group.Key,
                        AutoSize = true,
                        Location = new System.Drawing.Point() { X = 16, Y = labelY }
                    };
                    panel1.Controls.Add(parameterMultiLabel);

                    if (rule.Regex != null ||
                        rule.PreventDuplicates != null ||
                        rule.Requirement != null)
                    {
                        var multiTextbox = new System.Windows.Forms.TextBox
                        {
                            Location = new System.Drawing.Point() { X = multiUiX, Y = labelY },
                            Name = prefix + group.Key
                        };
                        multiTextbox.TextChanged += MultiTextbox_TextChanged;
                        panel1.Controls.Add(multiTextbox);

                        dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                        {
                            DataPropertyName = group.Key,
                            HeaderText = group.Key,
                            Name = PARAM + group.Key,
                            ReadOnly = false,
                            ToolTipText = rule.UserMessage
                        });
                    }
                    else if (rule.ListOptions != null || rule.KeyValues != null)
                    {
                        List<string> dataSource = new List<string>();
                        if (rule.ListOptions != null)
                        {
                            dataSource = rule.ListOptions.ConvertAll(q => q.Name);
                        }
                        else if (rule.KeyValues != null)
                        {
                            dataSource = rule.KeyValues.ConvertAll(q => q[0]);
                        }
                        var multiCbo = new System.Windows.Forms.ComboBox
                        {
                            Location = new System.Drawing.Point() { X = multiUiX, Y = labelY },
                            DataSource = dataSource,
                            DropDownStyle = ComboBoxStyle.DropDownList,
                            Name = prefix + group.Key
                        };
                        multiCbo.SelectedIndexChanged += MultiCbo_SelectedIndexChanged;
                        panel1.Controls.Add(multiCbo);

                        var cbo = new DataGridViewComboBoxColumn
                        {
                            Name = PARAM + group.Key,
                            HeaderText = group.Key,
                            DataSource = dataSource,
                            ToolTipText = rule.UserMessage,
                            FlatStyle = FlatStyle.Flat
                        };
                        dataGridView1.Columns.Add(cbo);
                    }
                }

                dataGridView1.DataSource = dt;

            }
            catch (Exception ex)
            {
                Utils.LogException("formGridList", ex);
            }

        }

        private const string prefix = "MULTIPARAM";

        private void MultiCbo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                TaskDialog.Show("Error", "No rows are selected");
                return;
            }
            var cbo = sender as System.Windows.Forms.ComboBox;
            var value = cbo.SelectedItem.ToString();
            var paramName = cbo.Name.Replace(prefix, "");
            foreach (var row in dataGridView1.SelectedRows.Cast<DataGridViewRow>())
            {
                row.Cells[PARAM + paramName].Value = value;
            }
        }

        private void MultiTextbox_TextChanged(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                TaskDialog.Show("Error", "No rows are selected");
                return;
            }
            var txt = sender as System.Windows.Forms.TextBox;
            var paramName = txt.Name.Replace(prefix, "");
            foreach (var row in dataGridView1.SelectedRows.Cast<DataGridViewRow>())
            {
                row.Cells[PARAM + paramName].Value = txt.Text;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            var parameterObjects = new List<ParameterObject>();
            var failures = new List<RuleFailure>();
            foreach (var row in dataGridView1.Rows)
            {
                var idValue = ((DataGridViewRow)row).Cells["Id"].Value;
                var id = new ElementId(int.Parse(idValue.ToString()));
                var element = Utils.doc.GetElement(id);
                var parameterStrings = new List<ParameterString>();
                foreach (var col in dataGridView1.Columns
                                    .Cast<DataGridViewColumn>()
                                    .Where(q => q.Name.StartsWith(PARAM)))
                {
                    var value = ((DataGridViewRow)row).Cells[col.Name].Value;
                    var parameterName = col.Name.Replace(PARAM, "");
                    if (value == null)
                    {
                        TaskDialog.Show("Error", "Must select a value for all elements");
                        return;
                    }

                    parameterStrings.Add(new ParameterString(element.LookupParameter(parameterName), value.ToString()));

                    var parameters = new List<Parameter> { Utils.GetParameter(element, parameterName) };
                    var item = new ParameterObject(parameters, value.ToString());
                    parameterObjects.Add(item);

                }
                var failuresForThisId = Utils.GetFailures(id, parameterStrings, out _);
                if (failuresForThisId.Any())
                {
                    failures.AddRange(failuresForThisId);
                }

            }
            if (failures.Count(q => q.FailureType == FailureType.PreventDuplicates) > 1 ||
                failures.Any(q => q.FailureType != FailureType.PreventDuplicates))
            {
                var td = new TaskDialog("Errors")
                {
                    MainInstruction = "Errors need to be resolved",
                    MainContent = string.Join(
                        Environment.NewLine,
                        failures.Select(q => $"{q.ElementId.IntegerValue} - {q.Rule.ParameterName} - {q.Rule.UserMessage}"))
                };
                td.Show();
            }
            else
            {
                Utils.eventHandlerWithParameterObject.Raise(parameterObjects);
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void btnShow_Click(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell == null)
                return;

            var row = dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex];
            var cell = row.Cells["Id"];
            var id = new ElementId(int.Parse(cell.Value.ToString()));
            var idList = new List<ElementId> { id };

            if (dataGridView1.SelectedRows.Count > 0)
            {
                var ids = dataGridView1.SelectedRows.Cast<DataGridViewRow>()
                    .Select(q => new ElementId(int.Parse(q.Cells["Id"].Value.ToString())))
                    .ToList();
                idList = ids;
            }
            var uidoc = new UIDocument(Utils.doc);
            uidoc.Selection.SetElementIds(idList);
            uidoc.ShowElements(idList);
            uidoc.RefreshActiveView();
            var uiview = uidoc.GetOpenUIViews().First(q => q.ViewId == uidoc.ActiveView.Id);
            uiview.Zoom(0.5);
        }

        private void dataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            //DataGridViewCellStyle style = new DataGridViewCellStyle
            //{
            //    BackColor = System.Drawing.Color.Black,
            //    ForeColor = System.Drawing.Color.Black
            //};

            //for (int r = 0; r < dataGridView1.Rows.Count; r++)
            //{
            //    for (int c = 0; c < dataGridView1.Columns.Count; c++)
            //    {
            //        var cell = dataGridView1.Rows[r].Cells[c];
            //        cell.Style = style;
                   
            //     //   var value = cell.Value;
            //     //   cell.ReadOnly = true;
            //    }
            //}
        }

        private void btnSelAll_Click(object sender, EventArgs e)
        {
            dataGridView1.SelectAll();
        }
    }
}
