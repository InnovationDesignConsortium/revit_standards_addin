namespace RevitDataValidator.Forms
{
    partial class FormEnableDisabledRules
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
            btnCancel = new System.Windows.Forms.Button();
            btnOK = new System.Windows.Forms.Button();
            lstRules = new System.Windows.Forms.CheckedListBox();
            SuspendLayout();
            // 
            // btnCancel
            // 
            btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnCancel.Location = new System.Drawing.Point(233, 404);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(112, 34);
            btnCancel.TabIndex = 0;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // btnOK
            // 
            btnOK.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnOK.Location = new System.Drawing.Point(115, 404);
            btnOK.Name = "btnOK";
            btnOK.Size = new System.Drawing.Size(112, 34);
            btnOK.TabIndex = 1;
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += btnOK_Click;
            // 
            // lstRules
            // 
            lstRules.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            lstRules.FormattingEnabled = true;
            lstRules.Location = new System.Drawing.Point(12, 12);
            lstRules.Name = "lstRules";
            lstRules.Size = new System.Drawing.Size(333, 368);
            lstRules.TabIndex = 2;
            // 
            // FormEnableDisabledRules
            // 
            AcceptButton = btnOK;
            AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new System.Drawing.Size(357, 450);
            ControlBox = false;
            Controls.Add(lstRules);
            Controls.Add(btnOK);
            Controls.Add(btnCancel);
            MinimizeBox = false;
            Name = "FormEnableDisabledRules";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Enable Rules";
            Load += FormEnableDisabledRules_Load;
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.CheckedListBox lstRules;
    }
}