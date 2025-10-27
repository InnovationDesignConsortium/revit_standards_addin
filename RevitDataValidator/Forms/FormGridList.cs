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
        private const int ComboBoxColumnIndex = 3;
        public FormGridList(List<RuleFailure> failures)
        {
            InitializeComponent();

            try
            {
                var dataTable = new DataTable
                {
                    TableName = "ResolveErrorsTable"
                };
                dataTable.Columns.Add("Category-b29938af");
                dataTable.Columns.Add("Family-b29938af");
                dataTable.Columns.Add("Name-b29938af");
                dataTable.Columns.Add("Parameter-b29938af");
                dataTable.Columns.Add("Id-b29938af");
                dataTable.Columns.Add("Message-b29938af");
                dataTable.Columns.Add("RuleName-b29938af");
                dataTable.Columns.Add("IsValueRequired-b29938af");
                dataTable.Columns.Add("RuleGuid-b29938af");

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
                    dataRow["Category-b29938af"] = element.Category.Name;
                    dataRow["Name-b29938af"] = element.Name;
                    dataRow["Id-b29938af"] = ElementIdExtension.GetValue(element.Id).ToString();
                    dataRow["Message-b29938af"] = ruleFailure.Rule.UserMessage;
                    dataRow["RuleName-b29938af"] = ruleFailure.Rule.RuleName;
                    dataRow["Parameter-b29938af"] = ruleFailure.Rule.ParameterName;
                    dataRow["IsValueRequired-b29938af"] = ruleFailure.Rule.IsValueRequired;
                    dataRow["RuleGuid-b29938af"] = ruleFailure.Rule.Guid;

                    if (element is FamilyInstance fi)
                    {
                        dataRow["Family-b29938af"] = fi.Symbol.Family.Name;
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
                    DataPropertyName = "Id-b29938af",
                    HeaderText = "Id",
                    Name = "Id-b29938af",
                    ReadOnly = true,
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Category-b29938af",
                    HeaderText = "Category",
                    ReadOnly = true,
                });

                if (hasFamily)
                {
                    dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        DataPropertyName = "Family-b29938af",
                        HeaderText = "Family",
                        ReadOnly = true,
                    });
                }

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Name-b29938af",
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
                            Name = prefix + group.Key,
                            Width = 250,
                            Anchor = AnchorStyles.Right | AnchorStyles.Left,
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
                            Width = 250,
                            Anchor = AnchorStyles.Right | AnchorStyles.Left,                            
                        };
                        multiCbo.SelectedIndexChanged += MultiCbo_SelectedIndexChanged;
                        panel1.Controls.Add(multiCbo);

                        var cbo = new DataGridViewComboBoxColumn
                        {
                            Name = PARAM + group.Key,
                            HeaderText = group.Key,
                            ToolTipText = rule.UserMessage,
                            FlatStyle = FlatStyle.Flat,
                            DataSource = dataSource
                        };
                        dataGridView1.Columns.Add(cbo);
                    }
                }

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Message-b29938af",
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
                    DataPropertyName = "RuleName-b29938af",
                    Name = "RuleName-b29938af",
                    ReadOnly = true,
                    Visible = false
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "Parameter-b29938af",
                    Name = "Parameter-b29938af",
                    ReadOnly = true,
                    Visible = true
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "IsValueRequired-b29938af",
                    Name = "IsValueRequired-b29938af",
                    ReadOnly = true,
                    Visible = false
                });

                dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = "RuleGuid-b29938af",
                    Name = "RuleGuid-b29938af",
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
                if (row.Cells[ComboBoxColumnIndex] is DataGridViewComboBoxCell dc)
                {
                    if (dc.Items.Count == 0)
                    {
                        SetComboBoxValues(row);
                    }
                }
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
                var idValue = row.Cells["Id-b29938af"].Value;
                var id = ElementIdUtils.New(int.Parse(idValue.ToString()));
                var element = Utils.doc.GetElement(id);
                var cells = row.Cells;
                var ruleName = cells["RuleName-b29938af"].Value?.ToString();

                var reasonForException = row.Cells["Exception"].Value?.ToString();
                var parameter = row.Cells["Parameter-b29938af"].Value.ToString();
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

                    var isValueRequired = bool.Parse(row.Cells["IsValueRequired-b29938af"].Value.ToString());
                    if (isValueRequired && value == null)
                    {
                        Autodesk.Revit.UI.TaskDialog.Show("Error", "Must select a value for all elements");
                        return;
                    }

                    if (value is DBNull)
                    {
                        value = "";
                    }
                    parameterStrings.Add(new ParameterString(element.LookupParameter(thisColumnParameterName), value?.ToString()));

                    var parameters = new List<Parameter> { Utils.GetParameter(element, thisColumnParameterName) };
                    var item = new ParameterObject(parameters, value?.ToString());
                    parameterObjects.Add(item);
                }
                var failuresForThisId = Utils.GetFailures(id, parameterStrings, WhenToRun.Realtime).Where(q => q.Rule.ParameterName == parameter).ToList();
                if (failuresForThisId.Count != 0)
                {
                    failures.AddRange(failuresForThisId);
                }
            }
            if (failures.Any())
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
            var cell = row.Cells["Id-b29938af"];
            var id = ElementIdUtils.New(int.Parse(cell.Value.ToString()));
            var idList = new List<ElementId> { id };

            if (dataGridView1.SelectedRows.Count > 0)
            {
                var ids = dataGridView1.SelectedRows.Cast<DataGridViewRow>()
                    .Select(q => ElementIdUtils.New(int.Parse(q.Cells["Id-b29938af"].Value.ToString())))
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
                    if (col.Name != PARAM + row.Cells["Parameter-b29938af"].Value &&
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

        private void SetComboBoxValues(DataGridViewRow row)
        {
            var elementId = ElementIdUtils.New(int.Parse(row.Cells["Id-b29938af"].Value.ToString()));
            var element = Utils.doc.GetElement(elementId);
            var rule = Utils.allParameterRules.First(q => q.Guid.ToString() == row.Cells["RuleGuid-b29938af"].Value.ToString());
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
            if (row.Cells[ComboBoxColumnIndex] is DataGridViewComboBoxCell dgvcbc)
            {
                dgvcbc.Items.Clear();
                foreach (object itemToAdd in dataSource)
                {
                    dgvcbc.Items.Add(itemToAdd);
                }
            }
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1)
            {
                return;
            }
            SetComboBoxValues(dataGridView1.Rows[e.RowIndex]);
        }

        private void dataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            Utils.Log($"{Text} {e.Exception.Message} ", LogLevel.Error);
        }
    }
}