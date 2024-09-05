﻿namespace RevitDataValidator.Forms
{
    partial class frmAbout
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
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            lblInstalled = new System.Windows.Forms.Label();
            lblNewest = new System.Windows.Forms.Label();
            btnClose = new System.Windows.Forms.Button();
            lbl3 = new System.Windows.Forms.Label();
            lblReleaseDate = new System.Windows.Forms.Label();
            btnDownload = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(17, 15);
            label1.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(145, 25);
            label1.TabIndex = 0;
            label1.Text = "Installed Version:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(17, 65);
            label2.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(202, 25);
            label2.TabIndex = 1;
            label2.Text = "Newest Relased Version:";
            // 
            // lblInstalled
            // 
            lblInstalled.AutoSize = true;
            lblInstalled.Location = new System.Drawing.Point(211, 15);
            lblInstalled.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            lblInstalled.Name = "lblInstalled";
            lblInstalled.Size = new System.Drawing.Size(20, 25);
            lblInstalled.TabIndex = 2;
            lblInstalled.Text = "x";
            // 
            // lblNewest
            // 
            lblNewest.AutoSize = true;
            lblNewest.Location = new System.Drawing.Point(214, 65);
            lblNewest.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            lblNewest.Name = "lblNewest";
            lblNewest.Size = new System.Drawing.Size(20, 25);
            lblNewest.TabIndex = 3;
            lblNewest.Text = "x";
            // 
            // btnClose
            // 
            btnClose.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnClose.Location = new System.Drawing.Point(390, 238);
            btnClose.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            btnClose.Name = "btnClose";
            btnClose.Size = new System.Drawing.Size(107, 38);
            btnClose.TabIndex = 4;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // lbl3
            // 
            lbl3.AutoSize = true;
            lbl3.Location = new System.Drawing.Point(17, 112);
            lbl3.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            lbl3.Name = "lbl3";
            lbl3.Size = new System.Drawing.Size(264, 25);
            lbl3.TabIndex = 5;
            lbl3.Text = "Release Date of Newest Version:";
            // 
            // lblReleaseDate
            // 
            lblReleaseDate.AutoSize = true;
            lblReleaseDate.Location = new System.Drawing.Point(273, 112);
            lblReleaseDate.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            lblReleaseDate.Name = "lblReleaseDate";
            lblReleaseDate.Size = new System.Drawing.Size(20, 25);
            lblReleaseDate.TabIndex = 6;
            lblReleaseDate.Text = "x";
            // 
            // btnDownload
            // 
            btnDownload.Location = new System.Drawing.Point(17, 155);
            btnDownload.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            btnDownload.Name = "btnDownload";
            btnDownload.Size = new System.Drawing.Size(440, 38);
            btnDownload.TabIndex = 7;
            btnDownload.Text = "Download Newest Version && Install on Exit";
            btnDownload.UseVisualStyleBackColor = true;
            // 
            // frmAbout
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(514, 297);
            Controls.Add(btnDownload);
            Controls.Add(lblReleaseDate);
            Controls.Add(lbl3);
            Controls.Add(btnClose);
            Controls.Add(lblNewest);
            Controls.Add(lblInstalled);
            Controls.Add(label2);
            Controls.Add(label1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            Name = "frmAbout";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "About";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label lblInstalled;
        private System.Windows.Forms.Label lblNewest;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lbl3;
        private System.Windows.Forms.Label lblReleaseDate;
        private System.Windows.Forms.Button btnDownload;
    }
}