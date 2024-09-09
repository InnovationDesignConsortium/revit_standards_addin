namespace RevitDataValidator.Forms
{
    partial class FormGridList
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnOk = new System.Windows.Forms.Button();
            dataGridView1 = new System.Windows.Forms.DataGridView();
            btnShow = new System.Windows.Forms.Button();
            panel1 = new System.Windows.Forms.Panel();
            btnSelAll = new System.Windows.Forms.Button();
            label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            panel1.SuspendLayout();
            SuspendLayout();
            // 
            // btnOk
            // 
            btnOk.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnOk.Location = new System.Drawing.Point(526, 413);
            btnOk.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            btnOk.Name = "btnOk";
            btnOk.Size = new System.Drawing.Size(79, 31);
            btnOk.TabIndex = 2;
            btnOk.Text = "OK";
            btnOk.UseVisualStyleBackColor = true;
            btnOk.Click += btnOk_Click;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(7, 7);
            dataGridView1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.RowHeadersWidth = 82;
            dataGridView1.RowTemplate.Height = 33;
            dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.Size = new System.Drawing.Size(598, 280);
            dataGridView1.TabIndex = 3;
            dataGridView1.DataBindingComplete += dataGridView1_DataBindingComplete;
            // 
            // btnShow
            // 
            btnShow.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            btnShow.Location = new System.Drawing.Point(7, 413);
            btnShow.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            btnShow.Name = "btnShow";
            btnShow.Size = new System.Drawing.Size(94, 31);
            btnShow.TabIndex = 5;
            btnShow.Text = "Show Element";
            btnShow.UseVisualStyleBackColor = true;
            btnShow.Click += btnShow_Click;
            // 
            // panel1
            // 
            panel1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panel1.AutoScroll = true;
            panel1.Controls.Add(btnSelAll);
            panel1.Controls.Add(label1);
            panel1.Location = new System.Drawing.Point(7, 291);
            panel1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(598, 112);
            panel1.TabIndex = 6;
            // 
            // btnSelAll
            // 
            btnSelAll.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnSelAll.Location = new System.Drawing.Point(519, 6);
            btnSelAll.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            btnSelAll.Name = "btnSelAll";
            btnSelAll.Size = new System.Drawing.Size(70, 24);
            btnSelAll.TabIndex = 1;
            btnSelAll.Text = "Select All";
            btnSelAll.UseVisualStyleBackColor = true;
            btnSelAll.Click += btnSelAll_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(9, 6);
            label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(174, 15);
            label1.TabIndex = 0;
            label1.Text = "Set Values For All Selected Rows";
            // 
            // FormGridList
            // 
            AcceptButton = btnOk;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(612, 451);
            ControlBox = false;
            Controls.Add(panel1);
            Controls.Add(btnShow);
            Controls.Add(dataGridView1);
            Controls.Add(btnOk);
            Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            Name = "FormGridList";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Resolve Rule Errors";
            TopMost = true;
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button btnShow;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnSelAll;
    }
}