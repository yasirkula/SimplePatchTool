using System;
using System.Collections.Generic;
using System.IO;

namespace SimplePatchToolCore
{
	internal class PatchIntercomms
	{
		public readonly SimplePatchTool Patcher;
		public readonly PatchDownloadManager DownloadManager;

		public readonly string RootPath;
		public string CachePath { get; private set; }
		public string DownloadsPath { get; private set; }
		public string DecompressedFilesPath { get; private set; }

		private VersionInfo m_versionInfo;
		public VersionInfo VersionInfo
		{
			get { return m_versionInfo; }
			set
			{
				m_versionInfo = value;
				CachePath = PatchUtils.GetPathWithTrailingSeparatorChar( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ) ) +
					"SimplePatchToolDls" + Path.DirectorySeparatorChar + value.Name + Path.DirectorySeparatorChar;
				DownloadsPath = CachePath + "Downloads" + Path.DirectorySeparatorChar;
				DecompressedFilesPath = CachePath + "Files" + Path.DirectorySeparatorChar;
			}
		}

		public SimplePatchTool.IListener Listener;

		public bool Cancel;
		public bool SilentMode;
		public bool LogProgress;
		public bool FileLogging;
		public bool VerifyFiles;

		private PatchStage m_stage;
		public PatchStage Stage
		{
			get { return m_stage; }
			set
			{
				m_stage = value;
				ListenerCallPatchStageChanged( value );
			}
		}

		public PatchFailReason FailReason;
		public string FailDetails;

		public bool SelfPatching { get { return Patcher.Operation == PatchOperation.SelfPatching; } }

		private readonly Queue<string> logs;
		private StreamWriter fileLogger;

		private readonly object progressLock;
		private IOperationProgress progress;
		private IOperationProgress overallProgress;

		public PatchIntercomms( SimplePatchTool patcher, string rootPath )
		{
			Patcher = patcher;
			RootPath = rootPath;

			logs = new Queue<string>();

			progressLock = new object();
			progress = null;
			overallProgress = null;

			DownloadManager = new PatchDownloadManager( this );

			Cancel = false;
			SilentMode = false;
			LogProgress = true;
			FileLogging = true;
			VerifyFiles = false;

			Stage = PatchStage.CheckingUpdates;
			FailReason = PatchFailReason.None;
			FailDetails = null;
		}

		~PatchIntercomms()
		{
			DisposeFileLogger();
		}

		public void Log( string log )
		{
			if( !SilentMode && !Cancel )
			{
				if( Listener != null && Listener.ReceiveLogs )
					ListenerCallLogReceived( log );
				else
				{
					lock( logs )
					{
						logs.Enqueue( log );
					}
				}
			}

			LogToFile( log );
		}

		// Log exceptions to the log file only
		public void LogToFile( Exception e )
		{
			if( FileLogging && fileLogger != null )
			{
				try
				{
					fileLogger.WriteLine( e.ToString() );
				}
				catch
				{
					DisposeFileLogger();
				}
			}
		}

		public void LogToFile( string log )
		{
			if( FileLogging && fileLogger != null )
			{
				try
				{
					fileLogger.WriteLine( log );
				}
				catch
				{
					DisposeFileLogger();
				}
			}
		}

		public void SetProgress( IOperationProgress progress )
		{
			if( !Cancel && LogProgress )
			{
				if( Listener != null && Listener.ReceiveProgress )
					ListenerCallProgressChanged( progress );
				else
				{
					lock( progressLock )
					{
						this.progress = progress;
					}
				}
			}
		}

		public void SetOverallProgress( IOperationProgress overallProgress )
		{
			if( !Cancel && LogProgress )
			{
				if( Listener != null && Listener.ReceiveProgress )
					ListenerCallOverallProgressChanged( overallProgress );
				else
				{
					lock( progressLock )
					{
						this.overallProgress = overallProgress;
					}
				}
			}
		}

		public string FetchLog()
		{
			lock( logs )
			{
				if( logs.Count == 0 )
					return null;

				return logs.Dequeue();
			}
		}

		public IOperationProgress FetchProgress()
		{
			IOperationProgress result = progress;
			if( progress != null )
			{
				lock( progressLock )
				{
					progress = null;
				}
			}

			return result;
		}

		public IOperationProgress FetchOverallProgress()
		{
			IOperationProgress result = overallProgress;
			if( overallProgress != null )
			{
				lock( progressLock )
				{
					overallProgress = null;
				}
			}

			return result;
		}

		public bool IsUnderMaintenance()
		{
			MaintenanceCheckResult underMaintenance = DownloadManager.CheckForMaintenance( VersionInfo.MaintenanceCheckURL );
			if( underMaintenance == MaintenanceCheckResult.Maintenance_AbortApp )
			{
				FailReason = PatchFailReason.UnderMaintenance_AbortApp;
				FailDetails = Localization.Get( StringId.E_ServersUnderMaintenance );

				return true;
			}
			else if( underMaintenance == MaintenanceCheckResult.Maintenance_CanLaunchApp )
			{
				FailReason = PatchFailReason.UnderMaintenance_CanLaunchApp;
				FailDetails = Localization.Get( StringId.E_ServersUnderMaintenance );

				return true;
			}

			if( Cancel )
				return true;

			return false;
		}

		public List<VersionItem> FindFilesToUpdate()
		{
			List<VersionItem> versionInfoFiles = VersionInfo.Files;
			List<VersionItem> result = new List<VersionItem>();
			for( int i = 0; i < versionInfoFiles.Count; i++ )
			{
				if( Cancel )
					return null;

				VersionItem item = versionInfoFiles[i];
				FileInfo localFile = new FileInfo( RootPath + item.Path );
				if( localFile.Exists && localFile.MatchesSignature( item.FileSize, item.Md5Hash ) )
					continue;

				if( SelfPatching )
				{
					FileInfo decompressedFile = new FileInfo( DecompressedFilesPath + item.Path );
					if( decompressedFile.Exists && decompressedFile.MatchesSignature( item.FileSize, item.Md5Hash ) )
						continue;
				}

				result.Add( item );
			}

			return result;
		}

		public string GetDownloadPathForPatch( string patchVersion )
		{
			return CachePath + patchVersion + PatchParameters.INCREMENTAL_PATCH_FILE_EXTENSION;
		}

		public string GetDecompressPathForPatch( string patchVersion )
		{
			return CachePath + patchVersion + Path.DirectorySeparatorChar;
		}

		public void UpdateVersion( VersionCode version )
		{
			PatchUtils.SetVersion( SelfPatching ? DecompressedFilesPath : RootPath, VersionInfo.Name, version );
		}

		public void InitializeFileLogger()
		{
			if( fileLogger != null )
				return;

			try
			{
				FileInfo logFile = new FileInfo( RootPath + PatchParameters.LOG_FILE_NAME );
				fileLogger = new StreamWriter( logFile.FullName, logFile.Exists && logFile.Length < PatchParameters.LOG_FILE_MAX_SIZE );
				fileLogger.WriteLine( string.Concat( Environment.NewLine, "=== ", DateTime.Now, " ===" ) );
			}
			catch
			{
				DisposeFileLogger();
			}
		}

		public void DisposeFileLogger()
		{
			if( fileLogger != null )
			{
				try
				{
					fileLogger.Close();
				}
				catch
				{
					fileLogger.Dispose();
				}

				fileLogger = null;
			}
		}

		#region Listener Methods
		public void ListenerCallStarted()
		{
			if( Listener != null )
			{
				try
				{
					Listener.Started();
				}
				catch { }
			}
		}

		public void ListenerCallLogReceived( string log )
		{
			if( Listener != null )
			{
				try
				{
					Listener.LogReceived( log );
				}
				catch { }
			}
		}

		public void ListenerCallProgressChanged( IOperationProgress progress )
		{
			if( Listener != null )
			{
				try
				{
					Listener.ProgressChanged( progress );
				}
				catch { }
			}
		}

		public void ListenerCallOverallProgressChanged( IOperationProgress progress )
		{
			if( Listener != null )
			{
				try
				{
					Listener.OverallProgressChanged( progress );
				}
				catch { }
			}
		}

		public void ListenerCallPatchStageChanged( PatchStage stage )
		{
			if( Listener != null )
			{
				try
				{
					Listener.PatchStageChanged( stage );
				}
				catch { }
			}
		}

		public void ListenerCallPatchMethodChanged( PatchMethod method )
		{
			if( Listener != null )
			{
				try
				{
					Listener.PatchMethodChanged( method );
				}
				catch { }
			}
		}

		public void ListenerCallVersionInfoFetched( VersionInfo versionInfo )
		{
			if( Listener != null )
			{
				try
				{
					Listener.VersionInfoFetched( versionInfo );
				}
				catch { }
			}
		}

		public void ListenerCallVersionFetched( string currentVersion, string newVersion )
		{
			if( Listener != null )
			{
				try
				{
					Listener.VersionFetched( currentVersion, newVersion );
				}
				catch { }
			}
		}

		public void ListenerCallFinished()
		{
			if( Listener != null )
			{
				try
				{
					Listener.Finished();
				}
				catch { }
			}
		}
		#endregion
	}
}