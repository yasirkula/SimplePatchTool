namespace SelfPatcherWindowsForms
{
	partial class SelfPatcherWindow
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
			this.closeButton = new System.Windows.Forms.Button();
			this.label = new System.Windows.Forms.Label();
			this.progressBar = new System.Windows.Forms.ProgressBar();
			this.SuspendLayout();
			// 
			// closeButton
			// 
			this.closeButton.Enabled = false;
			this.closeButton.Location = new System.Drawing.Point(277, 109);
			this.closeButton.Name = "closeButton";
			this.closeButton.Size = new System.Drawing.Size(100, 39);
			this.closeButton.TabIndex = 0;
			this.closeButton.Text = "OK";
			this.closeButton.UseMnemonic = false;
			this.closeButton.UseVisualStyleBackColor = true;
			// 
			// label
			// 
			this.label.Dock = System.Windows.Forms.DockStyle.Top;
			this.label.Location = new System.Drawing.Point(35, 10);
			this.label.Name = "label";
			this.label.Size = new System.Drawing.Size(342, 62);
			this.label.TabIndex = 1;
			this.label.Text = "Updating the app, please wait";
			this.label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// progressBar
			// 
			this.progressBar.Location = new System.Drawing.Point(38, 75);
			this.progressBar.Name = "progressBar";
			this.progressBar.Size = new System.Drawing.Size(339, 18);
			this.progressBar.TabIndex = 2;
			// 
			// SelfPatcherWindow
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(412, 165);
			this.ControlBox = false;
			this.Controls.Add(this.progressBar);
			this.Controls.Add(this.label);
			this.Controls.Add(this.closeButton);
			this.MaximizeBox = false;
			this.MaximumSize = new System.Drawing.Size(430, 212);
			this.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(430, 212);
			this.Name = "SelfPatcherWindow";
			this.Padding = new System.Windows.Forms.Padding(35, 10, 35, 10);
			this.ShowIcon = false;
			this.Text = " Updating...";
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Button closeButton;
		private System.Windows.Forms.Label label;
		private System.Windows.Forms.ProgressBar progressBar;
	}
}

