using SimplePatchToolCore;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace LauncherWinForms
{
	public partial class LauncherWindow : Form
	{
		// You should update these constants
		private const string LAUNCHER_VERSIONINFO_URL = ""; // see: https://github.com/yasirkula/SimplePatchTool/wiki/Generating-versionInfoURL
		private const string MAINAPP_VERSIONINFO_URL = ""; // see: https://github.com/yasirkula/SimplePatchTool/wiki/Generating-versionInfoURL
		private const string MAINAPP_SUBDIRECTORY = "MainApp";
		private const string MAINAPP_EXECUTABLE = "MainApp.exe"; // Main app executable is located at {APPLICATION_DIRECTORY}/MainApp/MainApp.exe
		private const string PATCH_NOTES_URL = "http://websitetips.com/articles/copy/lorem/ipsum.txt";

		private string launcherDirectory;
		private string mainAppDirectory;
		private string selfPatcherExecutablePath;

		private SimplePatchTool patcher;
		private bool isPatchingLauncher;

		private delegate void InvokeDelegate();

		public LauncherWindow()
		{
			InitializeComponent();

			launcherDirectory = Path.GetDirectoryName( PatchUtils.GetCurrentExecutablePath() );
			mainAppDirectory = Path.Combine( launcherDirectory, MAINAPP_SUBDIRECTORY );
			selfPatcherExecutablePath = Path.Combine( launcherDirectory, PatchParameters.SELF_PATCHER_DIRECTORY + Path.DirectorySeparatorChar + "SelfPatcher.exe" );

			patchNotesText.Text = string.Empty;
			statusText.Text = string.Empty;
			progressText.Text = string.Empty;
			progressBar.Value = 0;
			patchButton.Enabled = false;

			playButton.Click += ( s, e ) => PlayButtonClicked();
			patchButton.Click += ( s, e ) => PatchButtonClicked();
			repairButton.Click += ( s, e ) => RepairButtonClicked();

			if( !string.IsNullOrEmpty( PATCH_NOTES_URL ) )
				FetchPatchNotes();

			if( !StartLauncherPatch() )
				StartMainAppPatch( true );
		}

		private void PatchButtonClicked()
		{
			ActiveControl = null; // to prevent patchNotesText from somehow gaining focus

			if( patcher != null && !patcher.IsRunning )
				ExecutePatch();
		}

		private void RepairButtonClicked()
		{
			StartMainAppPatch( false );
		}

		private void PlayButtonClicked()
		{
			if( patcher != null && patcher.IsRunning && patcher.Operation != PatchOperation.CheckingForUpdates )
				return;

			FileInfo mainApp = new FileInfo( Path.Combine( mainAppDirectory, MAINAPP_EXECUTABLE ) );
			if( mainApp.Exists )
			{
				Process.Start( new ProcessStartInfo( mainApp.FullName ) { WorkingDirectory = mainApp.DirectoryName } );
				Close();
			}
			else
				UpdateLabel( statusText, MAINAPP_EXECUTABLE + " does not exist!" );
		}

		private bool StartLauncherPatch()
		{
			if( string.IsNullOrEmpty( LAUNCHER_VERSIONINFO_URL ) )
				return false;

			if( patcher != null && patcher.IsRunning )
				return false;

			isPatchingLauncher = true;

			patcher = new SimplePatchTool( launcherDirectory, LAUNCHER_VERSIONINFO_URL ).UseRepairPatch( true ).UseIncrementalPatch( true );
			CheckForUpdates( false );

			return true;
		}

		private bool StartMainAppPatch( bool checkForUpdates )
		{
			if( string.IsNullOrEmpty( MAINAPP_VERSIONINFO_URL ) )
				return false;

			if( patcher != null && patcher.IsRunning )
				return false;

			isPatchingLauncher = false;

			patcher = new SimplePatchTool( mainAppDirectory, MAINAPP_VERSIONINFO_URL ).UseRepairPatch( true ).UseIncrementalPatch( true );

			if( checkForUpdates )
				CheckForUpdates( true );
			else
				ExecutePatch();

			return true;
		}

		// = checkVersionOnly =
		// true (default): only version number (e.g. 1.0) is compared against VersionInfo to see if there is an update
		// false: hashes and sizes of the local files are compared against VersionInfo (if there are any different/missing files, we'll patch the app)
		private void CheckForUpdates( bool checkVersionOnly )
		{
			StartThread( () =>
			{
				ButtonSetEnabled( patchButton, false );
				ButtonSetEnabled( playButton, true );

				patcher.LogProgress( false );
				if( patcher.CheckForUpdates( checkVersionOnly ) )
				{
					while( patcher.IsRunning )
					{
						FetchLogsFromPatcher();
						Thread.Sleep( 500 );
					}

					FetchLogsFromPatcher();

					if( patcher.Result == PatchResult.AlreadyUpToDate )
					{
						// If launcher is already up-to-date, check if there is an update for the main app
						if( isPatchingLauncher )
							StartMainAppPatch( true );
					}
					else if( patcher.Result == PatchResult.Success )
					{
						// There is an update, enable the Patch button
						ButtonSetEnabled( patchButton, true );
					}
					else
					{
						// An error occurred, user can click the Patch button to try again
						ButtonSetEnabled( patchButton, true );
					}
				}
			} );
		}

		private void ExecutePatch()
		{
			StartThread( () =>
			{
				ButtonSetEnabled( patchButton, false );
				ButtonSetEnabled( playButton, false );

				patcher.LogProgress( true );
				if( patcher.Run( isPatchingLauncher ) )
				{
					while( patcher.IsRunning )
					{
						FetchLogsFromPatcher();
						Thread.Sleep( 500 );
					}

					FetchLogsFromPatcher();
					ButtonSetEnabled( playButton, true );

					if( patcher.Result == PatchResult.AlreadyUpToDate )
					{
						// If launcher is already up-to-date, check if there is an update for the main app
						if( isPatchingLauncher )
							StartMainAppPatch( true );
					}
					else if( patcher.Result == PatchResult.Success )
					{
						// If patcher was self patching the launcher, start the self patcher executable
						// Otherwise, we have just updated the main app successfully!
						if( patcher.Operation == PatchOperation.SelfPatching )
						{
							if( !string.IsNullOrEmpty( selfPatcherExecutablePath ) && File.Exists( selfPatcherExecutablePath ) )
								patcher.ApplySelfPatch( selfPatcherExecutablePath, PatchUtils.GetCurrentExecutablePath() );
							else
								UpdateLabel( progressText, "Self patcher does not exist!" );
						}
					}
					else
					{
						// An error occurred, user can click the Patch button to try again
						ButtonSetEnabled( patchButton, true );
					}
				}
			} );
		}

		private void FetchLogsFromPatcher()
		{
			string log = patcher.FetchLog();
			while( log != null )
			{
				UpdateLabel( statusText, log );
				log = patcher.FetchLog();
			}

			IOperationProgress progress = patcher.FetchProgress();
			while( progress != null )
			{
				UpdateLabel( progressText, progress.ProgressInfo );
				UpdateProgressbar( progressBar, progress.Percentage );

				progress = patcher.FetchProgress();
			}
		}

		private void FetchPatchNotes()
		{
			try
			{
				WebClient webClient = new WebClient();
				webClient.DownloadStringCompleted += ( sender, args ) =>
				{
					if( !args.Cancelled && args.Error == null )
						UpdateLabel( patchNotesText, args.Result );
				};

				webClient.DownloadStringAsync( new System.Uri( PATCH_NOTES_URL ) );
			}
			catch { }
		}

		private void StartThread( ThreadStart function )
		{
			Thread thread = new Thread( new ThreadStart( function ) ) { IsBackground = true };
			thread.Start();
		}

		private void ButtonSetEnabled( Button button, bool isEnabled )
		{
			RunOnUiThread( button, () => button.Enabled = isEnabled );
		}

		private void UpdateLabel( Control label, string text )
		{
			RunOnUiThread( label, () => label.Text = text );
		}

		private void UpdateProgressbar( ProgressBar progressBar, int value )
		{
			RunOnUiThread( progressBar, () => progressBar.Value = value );
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