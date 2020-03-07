using SimplePatchToolCore;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace LauncherWinForms
{
	public partial class LauncherWindow : Form
	{
		// UPDATE THESE CONSTANTS
		private const string LAUNCHER_VERSIONINFO_URL = ""; // see: https://github.com/yasirkula/SimplePatchTool/wiki/Generating-versionInfoURL
		private const string MAINAPP_VERSIONINFO_URL = ""; // see: https://github.com/yasirkula/SimplePatchTool/wiki/Generating-versionInfoURL
		private const string MAINAPP_SUBDIRECTORY = "MainApp";
		private const string MAINAPP_EXECUTABLE = "MainApp.exe"; // Main app executable will be located at {APPLICATION_DIRECTORY}/MainApp/MainApp.exe
		private const string SELF_PATCHER_EXECUTABLE = "SelfPatcher.exe"; // Self patcher executable will be located at {APPLICATION_DIRECTORY}/{PatchParameters.SELF_PATCHER_DIRECTORY}/SelfPatcher.exe
		private const string PATCH_NOTES_URL = "http://websitetips.com/articles/copy/lorem/ipsum.txt";

		private readonly string launcherDirectory;
		private readonly string mainAppDirectory;
		private readonly string selfPatcherPath;

		private SimplePatchTool patcher;
		private PatcherAsyncListener patcherListener;

		private bool isPatchingLauncher;

		private delegate void InvokeDelegate();

		public LauncherWindow()
		{
			InitializeComponent();

			launcherDirectory = Path.GetDirectoryName( PatchUtils.GetCurrentExecutablePath() );
			mainAppDirectory = Path.Combine( launcherDirectory, MAINAPP_SUBDIRECTORY );
			selfPatcherPath = PatchUtils.GetDefaultSelfPatcherExecutablePath( SELF_PATCHER_EXECUTABLE );

			patchNotesText.Text = string.Empty;
			statusText.Text = string.Empty;
			progressText.Text = string.Empty;
			progressBar.Value = 0;
			overallProgressBar.Value = 0;
			patchButton.Enabled = false;

			string currentVersion = PatchUtils.GetCurrentAppVersion();
			versionLabel.Text = string.IsNullOrEmpty( currentVersion ) ? "" : ( "v" + currentVersion );

			playButton.Click += ( s, e ) => PlayButtonClicked();
			patchButton.Click += ( s, e ) => PatchButtonClicked();
			repairButton.Click += ( s, e ) => RepairButtonClicked();

			if( !string.IsNullOrEmpty( PATCH_NOTES_URL ) )
				FetchPatchNotes();

			patcherListener = new PatcherAsyncListener();
			patcherListener.OnLogReceived += ( log ) => UpdateLabel( statusText, log );
			patcherListener.OnProgressChanged += ( progress ) =>
			{
				UpdateLabel( progressText, progress.ProgressInfo );
				UpdateProgressbar( progressBar, progress.Percentage );
			};
			patcherListener.OnOverallProgressChanged += ( progress ) => UpdateProgressbar( overallProgressBar, progress.Percentage );
			patcherListener.OnVersionInfoFetched += ( versionInfo ) =>
			{
				if( isPatchingLauncher )
					versionInfo.AddIgnoredPath( MAINAPP_SUBDIRECTORY + "/" );
			};
			patcherListener.OnVersionFetched += ( currVersion, newVersion ) =>
			{
				if( isPatchingLauncher )
					UpdateLabel( versionLabel, "v" + currVersion );
			};
			patcherListener.OnFinish += () =>
			{
				if( patcher.Operation == PatchOperation.CheckingForUpdates )
					CheckForUpdatesFinished();
				else
					PatchFinished();
			};

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
				UpdateLabel( statusText, Localization.Get( StringId.E_XDoesNotExist, MAINAPP_EXECUTABLE ) );
		}

		private bool StartLauncherPatch()
		{
			if( string.IsNullOrEmpty( LAUNCHER_VERSIONINFO_URL ) )
				return false;

			if( patcher != null && patcher.IsRunning )
				return false;

			isPatchingLauncher = true;

			patcher = new SimplePatchTool( launcherDirectory, LAUNCHER_VERSIONINFO_URL ).SetListener( patcherListener );
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

			patcher = new SimplePatchTool( mainAppDirectory, MAINAPP_VERSIONINFO_URL ).SetListener( patcherListener );

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
			if( patcher.CheckForUpdates( checkVersionOnly ) )
			{
				ButtonSetEnabled( patchButton, false );
				ButtonSetEnabled( playButton, true );
			}
		}

		private void ExecutePatch()
		{
			if( patcher.Operation == PatchOperation.ApplyingSelfPatch )
				ApplySelfPatch();
			else if( patcher.Run( isPatchingLauncher ) )
			{
				ButtonSetEnabled( patchButton, false );
				ButtonSetEnabled( playButton, false );
			}
		}

		private void ApplySelfPatch()
		{
			patcher.ApplySelfPatch( selfPatcherPath, PatchUtils.GetCurrentExecutablePath() );
		}

		private void CheckForUpdatesFinished()
		{
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

		private void PatchFinished()
		{
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
				// Otherwise, we have just updated the main app successfully
				if( patcher.Operation == PatchOperation.SelfPatching )
					ApplySelfPatch();
			}
			else
			{
				// An error occurred, user can click the Patch button to try again
				ButtonSetEnabled( patchButton, true );
			}
		}

		private void FetchPatchNotes()
		{
			WebClient webClient = null;
			try
			{
				webClient = new WebClient();
				webClient.DownloadStringCompleted += ( sender, args ) =>
				{
					if( !args.Cancelled && args.Error == null )
						UpdateLabel( patchNotesText, args.Result );

					webClient.Dispose();
				};

				webClient.DownloadStringAsync( new System.Uri( PATCH_NOTES_URL ) );
			}
			catch
			{
				if( webClient != null )
					webClient.Dispose();
			}
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
			if( value < 0 )
				value = 0;
			else if( value > 100 )
				value = 100;

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