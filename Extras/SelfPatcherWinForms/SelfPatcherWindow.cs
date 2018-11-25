using SelfPatcherCore;
using System.Threading;
using System.Windows.Forms;

namespace SelfPatcherWindowsForms
{
	public partial class SelfPatcherWindow : Form, ISelfPatcherListener
	{
		private readonly SelfPatcher selfPatcher;
		private readonly string[] args;

		private delegate void InvokeDelegate();

		public SelfPatcherWindow( string[] args )
		{
			InitializeComponent();

			closeButton.Click += ( s, e ) => Close();

			selfPatcher = new SelfPatcher( this );
			this.args = args;

			Thread selfPatcherThread = new Thread( new ThreadStart( SelfPatcherThread ) ) { IsBackground = false };
			selfPatcherThread.Start();
		}

		private void SelfPatcherThread()
		{
			selfPatcher.Run( args );
		}

		public void OnFail( string message )
		{
			if( !closeButton.IsHandleCreated )
				return;

			RunOnUiThread( label, () =>
			{
				label.Text = message;
				closeButton.Enabled = true;
			} );
		}

		public void OnLogAppeared( string message )
		{
			RunOnUiThread( label, () =>
			{
				label.Text = message;
			} );
		}

		public void OnProgressChanged( int currentInstruction, int numberOfInstructions )
		{
			RunOnUiThread( progressBar, () =>
			{
				progressBar.Value = currentInstruction;
				progressBar.Maximum = numberOfInstructions;
			} );
		}

		public void OnSuccess()
		{
			selfPatcher.ExecutePostSelfPatcher();
		}

		private void RunOnUiThread( Control control, InvokeDelegate function )
		{
			if( !control.IsHandleCreated )
				return;

			if( control.InvokeRequired )
				control.BeginInvoke( function );
			else
				function();
		}
	}
}