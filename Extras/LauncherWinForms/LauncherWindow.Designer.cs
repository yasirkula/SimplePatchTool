namespace LauncherWinForms
{
	partial class LauncherWindow
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if( disposing && ( components != null ) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.progressBar = new System.Windows.Forms.ProgressBar();
			this.patchButton = new System.Windows.Forms.Button();
			this.statusText = new System.Windows.Forms.Label();
			this.bottomPanel = new System.Windows.Forms.TableLayoutPanel();
			this.overallProgressBar = new System.Windows.Forms.ProgressBar();
			this.progressText = new System.Windows.Forms.Label();
			this.topPanel = new System.Windows.Forms.TableLayoutPanel();
			this.patchNotesGroup = new System.Windows.Forms.GroupBox();
			this.patchNotesText = new System.Windows.Forms.TextBox();
			this.playButton = new System.Windows.Forms.Button();
			this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
			this.versionLabel = new System.Windows.Forms.Label();
			this.repairButton = new System.Windows.Forms.Label();
			this.bottomPanel.SuspendLayout();
			this.topPanel.SuspendLayout();
			this.patchNotesGroup.SuspendLayout();
			this.tableLayoutPanel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// progressBar
			// 
			this.progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
			this.progressBar.Location = new System.Drawing.Point(13, 65);
			this.progressBar.Margin = new System.Windows.Forms.Padding(3, 3, 6, 3);
			this.progressBar.Name = "progressBar";
			this.progressBar.Size = new System.Drawing.Size(578, 10);
			this.progressBar.TabIndex = 1;
			this.progressBar.Value = 33;
			// 
			// patchButton
			// 
			this.patchButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(107)))), ((int)(((byte)(141)))));
			this.patchButton.Dock = System.Windows.Forms.DockStyle.Fill;
			this.patchButton.FlatAppearance.BorderSize = 0;
			this.patchButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.patchButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.patchButton.ForeColor = System.Drawing.Color.LightGray;
			this.patchButton.Location = new System.Drawing.Point(603, 8);
			this.patchButton.Margin = new System.Windows.Forms.Padding(6, 3, 3, 3);
			this.patchButton.Name = "patchButton";
			this.bottomPanel.SetRowSpan(this.patchButton, 4);
			this.patchButton.Size = new System.Drawing.Size(166, 89);
			this.patchButton.TabIndex = 0;
			this.patchButton.Text = "UPDATE";
			this.patchButton.UseVisualStyleBackColor = false;
			// 
			// statusText
			// 
			this.statusText.Dock = System.Windows.Forms.DockStyle.Fill;
			this.statusText.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.statusText.ForeColor = System.Drawing.SystemColors.HighlightText;
			this.statusText.Location = new System.Drawing.Point(13, 5);
			this.statusText.Margin = new System.Windows.Forms.Padding(3, 0, 6, 0);
			this.statusText.Name = "statusText";
			this.statusText.Size = new System.Drawing.Size(578, 37);
			this.statusText.TabIndex = 2;
			this.statusText.Text = "...Checking for updates...";
			this.statusText.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// bottomPanel
			// 
			this.bottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
			this.bottomPanel.ColumnCount = 2;
			this.bottomPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.bottomPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 175F));
			this.bottomPanel.Controls.Add(this.overallProgressBar, 0, 3);
			this.bottomPanel.Controls.Add(this.patchButton, 1, 0);
			this.bottomPanel.Controls.Add(this.statusText, 0, 0);
			this.bottomPanel.Controls.Add(this.progressBar, 0, 2);
			this.bottomPanel.Controls.Add(this.progressText, 0, 1);
			this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.bottomPanel.Location = new System.Drawing.Point(0, 348);
			this.bottomPanel.Name = "bottomPanel";
			this.bottomPanel.Padding = new System.Windows.Forms.Padding(10, 5, 10, 5);
			this.bottomPanel.RowCount = 4;
			this.bottomPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.bottomPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.bottomPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 16F));
			this.bottomPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
			this.bottomPanel.Size = new System.Drawing.Size(782, 105);
			this.bottomPanel.TabIndex = 3;
			// 
			// overallProgressBar
			// 
			this.overallProgressBar.Dock = System.Windows.Forms.DockStyle.Fill;
			this.overallProgressBar.Location = new System.Drawing.Point(13, 81);
			this.overallProgressBar.Margin = new System.Windows.Forms.Padding(3, 3, 6, 3);
			this.overallProgressBar.Name = "overallProgressBar";
			this.overallProgressBar.Size = new System.Drawing.Size(578, 16);
			this.overallProgressBar.TabIndex = 4;
			this.overallProgressBar.Value = 66;
			// 
			// progressText
			// 
			this.progressText.Dock = System.Windows.Forms.DockStyle.Fill;
			this.progressText.ForeColor = System.Drawing.SystemColors.HighlightText;
			this.progressText.Location = new System.Drawing.Point(13, 42);
			this.progressText.Margin = new System.Windows.Forms.Padding(3, 0, 6, 0);
			this.progressText.Name = "progressText";
			this.progressText.Size = new System.Drawing.Size(578, 20);
			this.progressText.TabIndex = 3;
			this.progressText.Text = "Downloading hodor.exe: 10.1/16.4MB (1.4 MB/s)";
			this.progressText.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// topPanel
			// 
			this.topPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(92)))), ((int)(((byte)(92)))), ((int)(((byte)(92)))));
			this.topPanel.ColumnCount = 1;
			this.topPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.topPanel.Controls.Add(this.patchNotesGroup, 0, 1);
			this.topPanel.Controls.Add(this.playButton, 0, 2);
			this.topPanel.Controls.Add(this.tableLayoutPanel1, 0, 0);
			this.topPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.topPanel.Location = new System.Drawing.Point(0, 0);
			this.topPanel.Name = "topPanel";
			this.topPanel.Padding = new System.Windows.Forms.Padding(6);
			this.topPanel.RowCount = 3;
			this.topPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.topPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.topPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
			this.topPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.topPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.topPanel.Size = new System.Drawing.Size(782, 348);
			this.topPanel.TabIndex = 4;
			// 
			// patchNotesGroup
			// 
			this.patchNotesGroup.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
			this.patchNotesGroup.Controls.Add(this.patchNotesText);
			this.patchNotesGroup.Dock = System.Windows.Forms.DockStyle.Fill;
			this.patchNotesGroup.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.patchNotesGroup.ForeColor = System.Drawing.SystemColors.HighlightText;
			this.patchNotesGroup.Location = new System.Drawing.Point(9, 29);
			this.patchNotesGroup.Margin = new System.Windows.Forms.Padding(3, 3, 3, 10);
			this.patchNotesGroup.Name = "patchNotesGroup";
			this.patchNotesGroup.Padding = new System.Windows.Forms.Padding(6, 6, 1, 2);
			this.patchNotesGroup.Size = new System.Drawing.Size(764, 217);
			this.patchNotesGroup.TabIndex = 5;
			this.patchNotesGroup.TabStop = false;
			this.patchNotesGroup.Text = "Patch Notes";
			// 
			// patchNotesText
			// 
			this.patchNotesText.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
			this.patchNotesText.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.patchNotesText.Dock = System.Windows.Forms.DockStyle.Fill;
			this.patchNotesText.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.patchNotesText.ForeColor = System.Drawing.SystemColors.HighlightText;
			this.patchNotesText.Location = new System.Drawing.Point(6, 21);
			this.patchNotesText.Multiline = true;
			this.patchNotesText.Name = "patchNotesText";
			this.patchNotesText.ReadOnly = true;
			this.patchNotesText.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.patchNotesText.Size = new System.Drawing.Size(757, 194);
			this.patchNotesText.TabIndex = 0;
			this.patchNotesText.Text = "  1.0.4r4\r\n\r\n- added lorem ipsum\r\n- dolor\r\n- hodor\r\n\r\n  1.1.2f3\r\n\r\n- added more l" +
    "orem ipsum";
			// 
			// playButton
			// 
			this.playButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(41)))), ((int)(((byte)(171)))), ((int)(((byte)(54)))));
			this.playButton.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.playButton.FlatAppearance.BorderSize = 0;
			this.playButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.playButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.playButton.ForeColor = System.Drawing.Color.LightGray;
			this.playButton.Location = new System.Drawing.Point(236, 259);
			this.playButton.Margin = new System.Windows.Forms.Padding(230, 3, 230, 6);
			this.playButton.Name = "playButton";
			this.playButton.Size = new System.Drawing.Size(310, 77);
			this.playButton.TabIndex = 4;
			this.playButton.Text = "PLAY";
			this.playButton.UseVisualStyleBackColor = false;
			// 
			// tableLayoutPanel1
			// 
			this.tableLayoutPanel1.ColumnCount = 2;
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
			this.tableLayoutPanel1.Controls.Add(this.versionLabel, 0, 0);
			this.tableLayoutPanel1.Controls.Add(this.repairButton, 1, 0);
			this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutPanel1.Location = new System.Drawing.Point(6, 6);
			this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
			this.tableLayoutPanel1.Name = "tableLayoutPanel1";
			this.tableLayoutPanel1.RowCount = 1;
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
			this.tableLayoutPanel1.Size = new System.Drawing.Size(770, 20);
			this.tableLayoutPanel1.TabIndex = 7;
			// 
			// versionLabel
			// 
			this.versionLabel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.versionLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.versionLabel.ForeColor = System.Drawing.Color.White;
			this.versionLabel.Location = new System.Drawing.Point(3, 0);
			this.versionLabel.Name = "versionLabel";
			this.versionLabel.Size = new System.Drawing.Size(379, 20);
			this.versionLabel.TabIndex = 8;
			this.versionLabel.Text = "v1.0";
			// 
			// repairButton
			// 
			this.repairButton.Dock = System.Windows.Forms.DockStyle.Fill;
			this.repairButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.repairButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
			this.repairButton.Location = new System.Drawing.Point(388, 0);
			this.repairButton.Name = "repairButton";
			this.repairButton.Size = new System.Drawing.Size(379, 20);
			this.repairButton.TabIndex = 7;
			this.repairButton.Text = "Repair Game";
			this.repairButton.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// LauncherWindow
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.SystemColors.ControlDark;
			this.ClientSize = new System.Drawing.Size(782, 453);
			this.Controls.Add(this.topPanel);
			this.Controls.Add(this.bottomPanel);
			this.Name = "LauncherWindow";
			this.ShowIcon = false;
			this.Text = "Launcher";
			this.bottomPanel.ResumeLayout(false);
			this.topPanel.ResumeLayout(false);
			this.patchNotesGroup.ResumeLayout(false);
			this.patchNotesGroup.PerformLayout();
			this.tableLayoutPanel1.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.Button patchButton;
		private System.Windows.Forms.ProgressBar progressBar;
		private System.Windows.Forms.Label statusText;
		private System.Windows.Forms.TableLayoutPanel bottomPanel;
		private System.Windows.Forms.Label progressText;
		private System.Windows.Forms.TableLayoutPanel topPanel;
		private System.Windows.Forms.GroupBox patchNotesGroup;
		private System.Windows.Forms.TextBox patchNotesText;
		private System.Windows.Forms.Button playButton;
		private System.Windows.Forms.ProgressBar overallProgressBar;
		private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
		private System.Windows.Forms.Label versionLabel;
		private System.Windows.Forms.Label repairButton;
	}
}

