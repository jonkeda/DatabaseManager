﻿namespace DatabaseManager
{
    partial class frmMain
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiSetting = new System.Windows.Forms.ToolStripMenuItem();
            this.tsmiDbConnection = new System.Windows.Forms.ToolStripMenuItem();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.navigator = new DatabaseManager.Controls.UC_DbObjectsNavigator();
            this.panelContent = new System.Windows.Forms.Panel();
            this.ucContent = new DatabaseManager.Controls.UC_DbObjectContent();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.tsslMessage = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.tsBtnGenerateScripts = new System.Windows.Forms.ToolStripButton();
            this.tsBtnConvert = new System.Windows.Forms.ToolStripButton();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panelContent.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.BackColor = System.Drawing.Color.White;
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem1});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(786, 25);
            this.menuStrip1.TabIndex = 5;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.BackColor = System.Drawing.Color.White;
            this.toolStripMenuItem1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.toolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsmiSetting,
            this.tsmiDbConnection});
            this.toolStripMenuItem1.Image = global::DatabaseManager.Properties.Resources.Tool16;
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(68, 21);
            this.toolStripMenuItem1.Text = "Tools";
            // 
            // tsmiSetting
            // 
            this.tsmiSetting.Image = global::DatabaseManager.Properties.Resources.Config;
            this.tsmiSetting.Name = "tsmiSetting";
            this.tsmiSetting.Size = new System.Drawing.Size(180, 22);
            this.tsmiSetting.Text = "Setting";
            this.tsmiSetting.Click += new System.EventHandler(this.tsmiSetting_Click);
            // 
            // tsmiDbConnection
            // 
            this.tsmiDbConnection.Image = global::DatabaseManager.Properties.Resources.DbConnect16;
            this.tsmiDbConnection.Name = "tsmiDbConnection";
            this.tsmiDbConnection.Size = new System.Drawing.Size(180, 22);
            this.tsmiDbConnection.Text = "Connection";
            this.tsmiDbConnection.Click += new System.EventHandler(this.tsmiDbConnection_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(0, 74);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.navigator);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.BackColor = System.Drawing.Color.White;
            this.splitContainer1.Panel2.Controls.Add(this.panelContent);
            this.splitContainer1.Size = new System.Drawing.Size(786, 372);
            this.splitContainer1.SplitterDistance = 150;
            this.splitContainer1.SplitterWidth = 3;
            this.splitContainer1.TabIndex = 8;
            // 
            // navigator
            // 
            this.navigator.Dock = System.Windows.Forms.DockStyle.Fill;
            this.navigator.Location = new System.Drawing.Point(0, 0);
            this.navigator.Name = "navigator";
            this.navigator.Size = new System.Drawing.Size(150, 372);
            this.navigator.TabIndex = 0;
            // 
            // panelContent
            // 
            this.panelContent.Controls.Add(this.ucContent);
            this.panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelContent.Location = new System.Drawing.Point(0, 0);
            this.panelContent.Name = "panelContent";
            this.panelContent.Size = new System.Drawing.Size(633, 372);
            this.panelContent.TabIndex = 0;
            // 
            // ucContent
            // 
            this.ucContent.BackColor = System.Drawing.SystemColors.Control;
            this.ucContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ucContent.Location = new System.Drawing.Point(0, 0);
            this.ucContent.Name = "ucContent";
            this.ucContent.Size = new System.Drawing.Size(633, 372);
            this.ucContent.TabIndex = 0;
            this.ucContent.Visible = false;
            // 
            // statusStrip
            // 
            this.statusStrip.AutoSize = false;
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsslMessage});
            this.statusStrip.Location = new System.Drawing.Point(0, 449);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(786, 18);
            this.statusStrip.TabIndex = 9;
            this.statusStrip.Text = "statusStrip1";
            // 
            // tsslMessage
            // 
            this.tsslMessage.AutoSize = false;
            this.tsslMessage.AutoToolTip = true;
            this.tsslMessage.Margin = new System.Windows.Forms.Padding(0);
            this.tsslMessage.Name = "tsslMessage";
            this.tsslMessage.Size = new System.Drawing.Size(500, 18);
            this.tsslMessage.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsBtnGenerateScripts,
            this.tsBtnConvert});
            this.toolStrip1.Location = new System.Drawing.Point(0, 25);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.toolStrip1.Size = new System.Drawing.Size(786, 43);
            this.toolStrip1.TabIndex = 10;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // tsBtnGenerateScripts
            // 
            this.tsBtnGenerateScripts.AutoSize = false;
            this.tsBtnGenerateScripts.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tsBtnGenerateScripts.Image = global::DatabaseManager.Properties.Resources.DbScripts;
            this.tsBtnGenerateScripts.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.tsBtnGenerateScripts.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tsBtnGenerateScripts.Name = "tsBtnGenerateScripts";
            this.tsBtnGenerateScripts.Size = new System.Drawing.Size(40, 40);
            this.tsBtnGenerateScripts.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.tsBtnGenerateScripts.ToolTipText = "Generate Scripts";
            this.tsBtnGenerateScripts.Click += new System.EventHandler(this.tsBtnGenerateScripts_Click);
            // 
            // tsBtnConvert
            // 
            this.tsBtnConvert.AutoSize = false;
            this.tsBtnConvert.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tsBtnConvert.Image = global::DatabaseManager.Properties.Resources.DbConvert;
            this.tsBtnConvert.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.tsBtnConvert.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tsBtnConvert.Name = "tsBtnConvert";
            this.tsBtnConvert.Size = new System.Drawing.Size(40, 40);
            this.tsBtnConvert.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.tsBtnConvert.ToolTipText = "Convert Database";
            this.tsBtnConvert.Click += new System.EventHandler(this.tsBtnConvert_Click);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(786, 467);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Name = "frmMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Database Manager";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.frmMain_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.frmMain_KeyDown);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panelContent.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem tsmiSetting;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private Controls.UC_DbObjectsNavigator navigator;
        private System.Windows.Forms.Panel panelContent;
        private Controls.UC_DbObjectContent ucContent;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel tsslMessage;
        private System.Windows.Forms.ToolStripMenuItem tsmiDbConnection;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton tsBtnGenerateScripts;
        private System.Windows.Forms.ToolStripButton tsBtnConvert;
    }
}

