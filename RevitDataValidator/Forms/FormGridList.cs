using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace RevitDataValidator.Forms
{
    public partial class FormGridList : System.Windows.Forms.Form
    {
        private const string PARAM = "PARAM";

        public FormGridList(List<RuleFailure> failures)
        {
            InitializeComponent();

            try
            {
                var dataTable = new DataTable
                {
                    TableName = "ResolveErrorsTable"
                };
                dataTable.Columns.Add("Category");
                dataTable.Columns.Add("Family");
                dataTable.Columns.Add("Name");
                dataTable.Columns.Add("Parameter");
                dataTable.Columns.Add("Id");
                dataTable.Columns.Add("Message");
                dataTable.Columns.Add("RuleName");
                dataTable.Columns.Add("IsValueRequired");
                dataTable.Columns.Add("RuleGuid");

                var rulesByParameterName = failures.GroupBy(q => q.Rule.ParameterName);
                foreach (var group in rulesByParameterName)
                {
                    dataTable.Columns.Add(group.Key);
                }

                var hasFamily = false;
                //var failuresOnePerElement = failures
                //  .GroupBy(p => p.ElementId)
                //  .Select(g => g.First())
                //  .ToList();

                foreach (var ruleFailure in failures)
                {
                    var dataRow = dataTable.NewRow();
                    var element = Utils.doc.GetElement(ruleFailure.ElementId);
                    dataRow["Category"] = element.Category.Name;
                    dataRow["Name"] = element.Name;
                    dataRow["Id"] = ElementIdExtension.GetValue(element.Id).ToString();
                    dataRow["Message"] = ruleFailure.Rule.UserMessage;
                    dataRow["RuleName"] = ruleFailure.Rule.RuleName;
                    dataRow["Parameter"] = ruleFailure.Rule.ParameterName;
                    dataRow["IsValueRequired"] = ruleFailure.Rule.IsValueRequired;
                    dataRow["RuleGuid"] = ruleFailure.Rule.Guid;

                    if (element is FamilyInstance fi)
                    {
                        dataRow["Family"] = fi.Symbol.Family.Name;
                        hasFamily = true;
                    }
                    foreach (var group in rulesByParameterName)
                    {
                        dataRow[group.Key] = Utils.GetParameter(element, group.Key).AsValueString();
                    }

                    dataTable.Rows.Add(dataRow);
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
                    else if (rule.ListOptions != null || rule.KeyValues != null || rule.DictKeyValues != null)
                    {
                        List<string> dataSource = null;
                        if (rule.ListOptions != null)
                        {
                            dataSource = rule.ListOptions.ConvertAll(q => q.Name);
                        }
                        else if (rule.KeyValues != null)
                        {
                            dataSource = rule.KeyValues.ConvertAll(q => q[0]);
                        }
                        else if (rule.DictKeyValues != null)
                        {
                            dataSource = Utils.GetKeyValuesFromFilterParameter(rule).ConvertAll(q => q[0]);
                        }
                        if (!rule.IsValueRequired)
                        {
                            dataSource.Add("");
                        }
                        var multiCbo = new System.Windows.Forms.ComboBox
                        {
                            Location = new System.Drawing.Point() { X = multiUiX, Y = labelY },
                            DataSource = dataSource,
                            DropDownStyle = ComboBoxStyle.DropDownList,
                            Name = prefix + group.Key,
                            Width = 350
                        };
                        multiCbo.SelectedIndexChanged += MultiCbo_SelectedIndexChanged;
                        panel1.Controls.Add(multiCbo);

                        var cbo = new DataGridViewComboBoxColumn
                        {
                            Name = PARAM + group.Key,
                            HeaderText = group.Key,
                            ToolTipText = rule.UserMessage,
                            FlatStyle = FlatStyle.Flat
                        };
                        dataGridView1.Columns.Add(cbo);
                    }
                }

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Message",
                    HeaderText = "Rule",
                    ReadOnly = true,
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Exception",
                    Name = "Exception",
                    HeaderText = "Reason For Exception",
                    ReadOnly = false,
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "RuleName",
                    Name = "RuleName",
                    ReadOnly = true,
                    Visible = false
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Parameter",
                    Name = "Parameter",
                    ReadOnly = true,
                    Visible = true
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "IsValueRequired",
                    Name = "IsValueRequired",
                    ReadOnly = true,
                    Visible = false
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "RuleGuid",
                    Name = "RuleGuid",
                    ReadOnly = true,
                    Visible = false
                });

                dataGridView1.DataSource = dataTable;
                dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
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
                Autodesk.Revit.UI.TaskDialog.Show("Error", "No rows are selected");
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
                Autodesk.Revit.UI.TaskDialog.Show("Error", "No rows are selected");
                return;
            }
            var txt = sender as System.Windows.Forms.TextBox;
            var paramName = txt.Name.Replace(prefix, "");
            foreach (var row in dataGridView1.SelectedRows.Cast<DataGridViewRow>())
            {
                var cell = row.Cells[PARAM + paramName];
                if (cell.ReadOnly)
                {
                    continue;
                }
                cell.Value = txt.Text;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            var parameterObjects = new List<ParameterObject>();
            var failures = new List<RuleFailure>();
            foreach (var row in dataGridView1.Rows.Cast<DataGridViewRow>())
            {
                var idValue = row.Cells["Id"].Value;
                var id = ElementIdUtils.New(int.Parse(idValue.ToString()));
                var element = Utils.doc.GetElement(id);
                var cells = row.Cells;
                var ruleName = cells["RuleName"].Value?.ToString();

                var reasonForException = row.Cells["Exception"].Value?.ToString();
                var parameter = row.Cells["Parameter"].Value.ToString();
                if (!string.IsNullOrEmpty(reasonForException?.ToString()))
                {
                    Utils.Log($"{ruleName}|EXCEPTION|{Utils.GetElementInfo(element)}|{parameter}|{reasonForException}", LogLevel.Warn);
                    Utils.SetReasonAllowed(element, ruleName, parameter, reasonForException);
                    continue;
                }

                var parameterStrings = new List<ParameterString>();
                foreach (var col in dataGridView1.Columns
                                    .Cast<DataGridViewColumn>()
                                    .Where(q => q.Name.StartsWith(PARAM)))
                {
                    var thisColumnParameterName = col.Name.Replace(PARAM, "");
                    if (thisColumnParameterName != parameter)
                    {
                        continue;
                    }
                    var value = row.Cells[col.Name].Value;

                    var isValueRequired = bool.Parse(row.Cells["IsValueRequired"].Value.ToString());
                    if (isValueRequired && value == null)
                    {
                        Autodesk.Revit.UI.TaskDialog.Show("Error", "Must select a value for all elements");
                        return;
                    }

                    parameterStrings.Add(new ParameterString(element.LookupParameter(thisColumnParameterName), value?.ToString()));

                    var parameters = new List<Parameter> { Utils.GetParameter(element, thisColumnParameterName) };
                    var item = new ParameterObject(parameters, value?.ToString());
                    parameterObjects.Add(item);
                }
                var failuresForThisId = Utils.GetFailures(id, parameterStrings, WhenToRun.Realtime, out _).Where(q => q.Rule.ParameterName == parameter).ToList();
                if (failuresForThisId.Count != 0)
                {
                    failures.AddRange(failuresForThisId);
                }
            }
            if (failures.Count(q => q.FailureType == FailureType.PreventDuplicates) > 1 ||
                failures.Any(q => q.FailureType != FailureType.PreventDuplicates))
            {
                var td = new Autodesk.Revit.UI.TaskDialog("Errors")
                {
                    MainInstruction = "Errors need to be resolved",
                    MainContent = string.Join(
                        Environment.NewLine,
                        failures.Select(q => $"{ElementIdExtension.GetValue(q.ElementId)} - {q.Rule.ParameterName} - {q.Rule.UserMessage}"))
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
            var id = ElementIdUtils.New(int.Parse(cell.Value.ToString()));
            var idList = new List<ElementId> { id };

            if (dataGridView1.SelectedRows.Count > 0)
            {
                var ids = dataGridView1.SelectedRows.Cast<DataGridViewRow>()
                    .Select(q => ElementIdUtils.New(int.Parse(q.Cells["Id"].Value.ToString())))
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
            for (int r = 0; r < dataGridView1.Rows.Count; r++)
            {
                var row = dataGridView1.Rows[r];
                for (int c = 0; c < dataGridView1.ColumnCount; c++)
                {
                    var col = dataGridView1.Columns[c];
                    if (col.Name != PARAM + row.Cells["Parameter"].Value &&
                        col.Name.StartsWith(PARAM)
                        )
                    {
                        row.Cells[c].ReadOnly = true;
                        row.Cells[c].Style.BackColor = System.Drawing.Color.Black;
                        row.Cells[c].Style.ForeColor = System.Drawing.Color.Black;
                    }
                }
            }
        }

        private void btnSelAll_Click(object sender, EventArgs e)
        {
            dataGridView1.SelectAll();
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1)
            {
                return;
            }
            var row = dataGridView1.Rows[e.RowIndex];
            var col = dataGridView1.Columns[e.ColumnIndex];
            var elementId = ElementIdUtils.New(int.Parse(row.Cells["Id"].Value.ToString()));
            var element = Utils.doc.GetElement(elementId);
            var rule = Utils.allParameterRules.First(q => q.Guid.ToString() == row.Cells["RuleGuid"].Value.ToString());
            var dataSource = new List<string>();
            if (rule.ListOptions != null)
            {
                dataSource = Utils.GetChoicesFromList(element, rule).Select(q => q.Name).ToList();
            }
            else if (rule.KeyValues != null)
            {
                dataSource = rule.KeyValues.ConvertAll(q => q[0]);
            }
            else if (rule.DictKeyValues != null)
            {
                dataSource = Utils.GetKeyValuesFromFilterParameter(rule).ConvertAll(q => q[0]);
            }
            if (!rule.IsValueRequired)
            {
                dataSource.Add("");
            }
            if (dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex] is DataGridViewComboBoxCell dgvcbc)
            {
                dgvcbc.Items.Clear();
                foreach (object itemToAdd in dataSource)
                {
                    dgvcbc.Items.Add(itemToAdd);
                }
            }
        }
    }
}