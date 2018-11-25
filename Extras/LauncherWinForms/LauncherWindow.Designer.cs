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
			this.patchNotesGroup = new System.Windows.Forms.GroupBox();
			this.patchNotesText = new System.Windows.Forms.TextBox();
			this.progressBar = new System.Windows.Forms.ProgressBar();
			this.patchButton = new System.Windows.Forms.Button();
			this.playButton = new System.Windows.Forms.Button();
			this.statusText = new System.Windows.Forms.Label();
			this.bottomPanel = new System.Windows.Forms.TableLayoutPanel();
			this.progressText = new System.Windows.Forms.Label();
			this.topPanel = new System.Windows.Forms.TableLayoutPanel();
			this.repairButton = new System.Windows.Forms.Label();
			this.patchNotesGroup.SuspendLayout();
			this.bottomPanel.SuspendLayout();
			this.topPanel.SuspendLayout();
			this.SuspendLayout();
			// 
			// patchNotesGroup
			// 
			this.patchNotesGroup.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
			this.patchNotesGroup.Controls.Add(this.patchNotesText);
			this.patchNotesGroup.Dock = System.Windows.Forms.DockStyle.Fill;
			this.patchNotesGroup.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.patchNotesGroup.ForeColor = System.Drawing.SystemColors.HighlightText;
			this.patchNotesGroup.Location = new System.Drawing.Point(9, 9);
			this.patchNotesGroup.Name = "patchNotesGroup";
			this.patchNotesGroup.Padding = new System.Windows.Forms.Padding(6, 6, 1, 2);
			this.topPanel.SetRowSpan(this.patchNotesGroup, 2);
			this.patchNotesGroup.Size = new System.Drawing.Size(415, 323);
			this.patchNotesGroup.TabIndex = 0;
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
			this.patchNotesText.Size = new System.Drawing.Size(408, 300);
			this.patchNotesText.TabIndex = 0;
			this.patchNotesText.Text = "  1.0.4r4\r\n\r\n- added lorem ipsum\r\n- dolor\r\n- hodor\r\n\r\n  1.1.2f3\r\n\r\n- added more l" +
    "orem ipsum";
			// 
			// progressBar
			// 
			this.progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
			this.progressBar.Location = new System.Drawing.Point(13, 68);
			this.progressBar.Margin = new System.Windows.Forms.Padding(3, 3, 6, 3);
			this.progressBar.Name = "progressBar";
			this.progressBar.Size = new System.Drawing.Size(487, 31);
			this.progressBar.TabIndex = 1;
			this.progressBar.Value = 33;
			// 
			// patchButton
			// 
			this.patchButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(107)))), ((int)(((byte)(141)))));
			this.patchButton.Dock = System.Windows.Forms.DockStyle.Fill;
			this.patchButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.patchButton.ForeColor = System.Drawing.Color.LightGray;
			this.patchButton.Location = new System.Drawing.Point(512, 13);
			this.patchButton.Margin = new System.Windows.Forms.Padding(6, 3, 3, 3);
			this.patchButton.Name = "patchButton";
			this.bottomPanel.SetRowSpan(this.patchButton, 3);
			this.patchButton.Size = new System.Drawing.Size(257, 86);
			this.patchButton.TabIndex = 0;
			this.patchButton.Text = "UPDATE";
			this.patchButton.UseVisualStyleBackColor = false;
			// 
			// playButton
			// 
			this.playButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(61)))), ((int)(((byte)(171)))), ((int)(((byte)(74)))));
			this.playButton.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.playButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.playButton.ForeColor = System.Drawing.Color.LightGray;
			this.playButton.Location = new System.Drawing.Point(430, 233);
			this.playButton.Name = "playButton";
			this.playButton.Size = new System.Drawing.Size(343, 99);
			this.playButton.TabIndex = 2;
			this.playButton.Text = "PLAY";
			this.playButton.UseVisualStyleBackColor = false;
			// 
			// statusText
			// 
			this.statusText.Dock = System.Windows.Forms.DockStyle.Fill;
			this.statusText.ForeColor = System.Drawing.SystemColors.HighlightText;
			this.statusText.Location = new System.Drawing.Point(13, 10);
			this.statusText.Margin = new System.Windows.Forms.Padding(3, 0, 6, 6);
			this.statusText.Name = "statusText";
			this.statusText.Size = new System.Drawing.Size(487, 26);
			this.statusText.TabIndex = 2;
			this.statusText.Text = "...Checking for updates...";
			this.statusText.TextAlign = System.Drawing.ContentAlignment.TopCenter;
			// 
			// bottomPanel
			// 
			this.bottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
			this.bottomPanel.ColumnCount = 2;
			this.bottomPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 65.1282F));
			this.bottomPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 34.8718F));
			this.bottomPanel.Controls.Add(this.patchButton, 1, 0);
			this.bottomPanel.Controls.Add(this.statusText, 0, 0);
			this.bottomPanel.Controls.Add(this.progressBar, 0, 2);
			this.bottomPanel.Controls.Add(this.progressText, 0, 1);
			this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.bottomPanel.Location = new System.Drawing.Point(0, 341);
			this.bottomPanel.Name = "bottomPanel";
			this.bottomPanel.Padding = new System.Windows.Forms.Padding(10);
			this.bottomPanel.RowCount = 3;
			this.bottomPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 35F));
			this.bottomPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
			this.bottomPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
			this.bottomPanel.Size = new System.Drawing.Size(782, 112);
			this.bottomPanel.TabIndex = 3;
			// 
			// progressText
			// 
			this.progressText.Dock = System.Windows.Forms.DockStyle.Fill;
			this.progressText.ForeColor = System.Drawing.SystemColors.HighlightText;
			this.progressText.Location = new System.Drawing.Point(13, 42);
			this.progressText.Margin = new System.Windows.Forms.Padding(3, 0, 6, 0);
			this.progressText.Name = "progressText";
			this.progressText.Size = new System.Drawing.Size(487, 23);
			this.progressText.TabIndex = 3;
			this.progressText.Text = "Downloading hodor.exe: 10.1/16.4MB (1.4 MB/s)";
			this.progressText.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// topPanel
			// 
			this.topPanel.BackColor = System.Drawing.Color.DimGray;
			this.topPanel.ColumnCount = 2;
			this.topPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 54.69543F));
			this.topPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45.30457F));
			this.topPanel.Controls.Add(this.patchNotesGroup, 0, 0);
			this.topPanel.Controls.Add(this.playButton, 1, 1);
			this.topPanel.Controls.Add(this.repairButton, 1, 0);
			this.topPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.topPanel.Location = new System.Drawing.Point(0, 0);
			this.topPanel.Name = "topPanel";
			this.topPanel.Padding = new System.Windows.Forms.Padding(6);
			this.topPanel.RowCount = 2;
			this.topPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.topPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.topPanel.Size = new System.Drawing.Size(782, 341);
			this.topPanel.TabIndex = 4;
			// 
			// repairButton
			// 
			this.repairButton.Dock = System.Windows.Forms.DockStyle.Fill;
			this.repairButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(162)));
			this.repairButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
			this.repairButton.Location = new System.Drawing.Point(430, 6);
			this.repairButton.Name = "repairButton";
			this.repairButton.Size = new System.Drawing.Size(343, 20);
			this.repairButton.TabIndex = 3;
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
			this.patchNotesGroup.ResumeLayout(false);
			this.patchNotesGroup.PerformLayout();
			this.bottomPanel.ResumeLayout(false);
			this.topPanel.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.GroupBox patchNotesGroup;
		private System.Windows.Forms.TextBox patchNotesText;
		private System.Windows.Forms.Button playButton;
		private System.Windows.Forms.Button patchButton;
		private System.Windows.Forms.ProgressBar progressBar;
		private System.Windows.Forms.Label statusText;
		private System.Windows.Forms.TableLayoutPanel bottomPanel;
		private System.Windows.Forms.Label progressText;
		private System.Windows.Forms.TableLayoutPanel topPanel;
		private System.Windows.Forms.Label repairButton;
	}
}

