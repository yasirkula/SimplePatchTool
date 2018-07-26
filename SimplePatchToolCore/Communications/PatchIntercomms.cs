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

				try
				{
					DisposeFileLogger();

					if( PatchUtils.CheckWriteAccessToFolder( CachePath ) )
					{
						FileInfo logFile = new FileInfo( CachePath + PatchParameters.LOG_FILE_NAME );
						if( logFile.Exists && logFile.Length < PatchParameters.LOG_FILE_MAX_SIZE )
							fileLogger = logFile.AppendText();
						else
							fileLogger = logFile.CreateText();

						fileLogger.WriteLine( string.Concat( "=== ", DateTime.UtcNow, " ===" ) );
					}
				}
				catch
				{
					DisposeFileLogger();
				}
			}
		}

		public bool Cancel;
		public bool SilentMode;
		public bool LogProgress;
		public bool LogToFile;
		public bool VerifyFiles;

		public PatchStage Stage;
		public PatchFailReason FailReason;
		public string FailDetails;

		public bool SelfPatching { get { return Patcher.Operation == PatchOperation.SelfPatching; } }

		private readonly Queue<string> logs;
		private IOperationProgress progress;
		private StreamWriter fileLogger;

		public PatchIntercomms( SimplePatchTool patcher, string rootPath )
		{
			Patcher = patcher;
			RootPath = rootPath;

			logs = new Queue<string>();
			progress = null;

			DownloadManager = new PatchDownloadManager( this );

			Cancel = false;
			SilentMode = false;
			LogProgress = true;
			LogToFile = true;
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
				lock( logs )
				{
					logs.Enqueue( log );
				}
			}

			if( LogToFile && fileLogger != null )
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
				this.progress = progress;
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
			if( progress != null && !progress.IsUsed )
			{
				progress.IsUsed = true;
				return progress;
			}

			return null;
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

		public string GetDownloadPathForPatch( string patchVersion )
		{
			return CachePath + patchVersion + PatchParameters.PATCH_FILE_EXTENSION;
		}

		public string GetDecompressPathForPatch( string patchVersion )
		{
			return CachePath + patchVersion + Path.DirectorySeparatorChar;
		}

		public void UpdateVersion( VersionCode version )
		{
			PatchUtils.SetVersion( SelfPatching ? DecompressedFilesPath : RootPath, VersionInfo.Name, version );
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
	}
}