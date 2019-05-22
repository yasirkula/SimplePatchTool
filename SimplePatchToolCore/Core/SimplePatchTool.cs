using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace SimplePatchToolCore
{
	public enum PatchOperation { Patching, SelfPatching, CheckingForUpdates }
	public enum PatchMethod { None, RepairPatch, IncrementalPatch, InstallerPatch }
	public enum PatchResult { Failed, Success, AlreadyUpToDate }
	public enum PatchStage
	{
		CheckingUpdates, CheckingFileIntegrity, DeletingObsoleteFiles,
		DownloadingFiles, ExtractingFilesFromArchive, VerifyingFilesOnServer,
		CalculatingFilesToUpdate, UpdatingFiles
	}

	public enum MaintenanceCheckResult { NoMaintenance, Maintenance_AbortApp, Maintenance_CanLaunchApp }

	public enum PatchFailReason
	{
		None, Cancelled, Unknown, FatalException,
		InsufficientSpace, RequiresAdminPriviledges, MultipleRunningInstances,
		NoSuitablePatchMethodFound, FilesAreNotUpToDateAfterPatch,
		UnderMaintenance_AbortApp, UnderMaintenance_CanLaunchApp,
		DownloadError, CorruptDownloadError,
		FileDoesNotExistOnServer, FileIsNotValidOnServer,
		XmlDeserializeError, InvalidVersionCode,
		CantVerifyVersionInfo, CantVerifyPatchInfo
	}

	public delegate IDownloadHandler DownloadHandlerFactory();
	public delegate long FreeDiskSpaceCalculator( string drive );
	public delegate bool XMLVerifier( ref string xmlContents );

	public class SimplePatchTool
	{
		public interface IListener
		{
			bool ReceiveLogs { get; }
			bool ReceiveProgress { get; }

			void Started();
			void LogReceived( string log );
			void ProgressChanged( IOperationProgress progress );
			void OverallProgressChanged( IOperationProgress progress );
			void PatchStageChanged( PatchStage stage );
			void PatchMethodChanged( PatchMethod method );
			void VersionInfoFetched( VersionInfo versionInfo );
			void VersionFetched( string currentVersion, string newVersion );
			void Finished();
		}

		private struct PatchMethodHolder
		{
			public readonly PatchMethod method;
			public readonly long size;

			public PatchMethodHolder( PatchMethod method, long size )
			{
				this.method = method;
				this.size = size;
			}
		}

		private readonly string[] ROOT_PATH_PLACEHOLDERS = new string[] { "{ROOT_PATH}", "{APPLICATION_DIRECTORY}" };

		private readonly PatchIntercomms comms;
		private readonly string versionInfoURL;

		private VersionCode currentVersion;

		private bool canRepairPatch;
		private bool canIncrementalPatch;
		private bool canInstallerPatch;

		private bool checkForMultipleRunningInstances;

		private readonly List<IncrementalPatch> incrementalPatches;
		private readonly List<IncrementalPatchInfo> incrementalPatchesInfo;
		private readonly HashSet<string> filesInVersion;

		public bool IsRunning { get; private set; }
		public PatchOperation Operation { get; private set; }
		public PatchMethod PatchMethod { get; private set; }
		public PatchResult Result { get; private set; }

		internal DownloadHandlerFactory DownloadHandlerFactory { get; private set; }
		internal FreeDiskSpaceCalculator FreeDiskSpaceCalculator { get; private set; }
		internal XMLVerifier VersionInfoVerifier { get; private set; }
		internal XMLVerifier PatchInfoVerifier { get; private set; }

		public string NewVersion { get { return comms.VersionInfo != null ? comms.VersionInfo.Version : null; } }

		public PatchStage PatchStage
		{
			get { return comms.Stage; }
			private set { comms.Stage = value; }
		}

		public PatchFailReason FailReason
		{
			get
			{
				if( IsRunning || Result != PatchResult.Failed )
					return PatchFailReason.None;

				if( comms.Cancel )
					return PatchFailReason.Cancelled;

				return comms.FailReason;
			}
			private set { comms.FailReason = value; }
		}

		public string FailDetails
		{
			get
			{
				if( IsRunning || Result != PatchResult.Failed )
					return null;

				if( comms.Cancel )
					return Localization.Get( StringId.Cancelled );

				return comms.FailDetails;
			}
			private set { comms.FailDetails = value; }
		}

		/// <exception cref = "ArgumentException">An argument is empty</exception>
		public SimplePatchTool( string rootPath, string versionInfoURL )
		{
			rootPath = rootPath.Trim();
			versionInfoURL = versionInfoURL.Trim();

			if( string.IsNullOrEmpty( rootPath ) )
				throw new ArgumentException( Localization.Get( StringId.E_XCanNotBeEmpty, "'rootPath'" ) );

			if( string.IsNullOrEmpty( versionInfoURL ) )
				throw new ArgumentException( Localization.Get( StringId.E_XCanNotBeEmpty, "'versionInfoURL'" ) );

			Localization.Get( StringId.Done ); // Force the localization system to be initialized with the current culture/language

			for( int i = 0; i < ROOT_PATH_PLACEHOLDERS.Length; i++ )
			{
				if( rootPath.IndexOf( ROOT_PATH_PLACEHOLDERS[i] ) >= 0 )
					rootPath.Replace( ROOT_PATH_PLACEHOLDERS[i], Path.GetDirectoryName( PatchUtils.GetCurrentExecutablePath() ) );
			}

			comms = new PatchIntercomms( this, PatchUtils.GetPathWithTrailingSeparatorChar( rootPath ) );
			this.versionInfoURL = versionInfoURL;

			canRepairPatch = true;
			canIncrementalPatch = true;
			canInstallerPatch = true;

			checkForMultipleRunningInstances = true;

			incrementalPatches = new List<IncrementalPatch>();
			incrementalPatchesInfo = new List<IncrementalPatchInfo>();
			filesInVersion = new HashSet<string>();

			UseCustomDownloadHandler( null );
			UseCustomFreeSpaceCalculator( null );

			IsRunning = false;
			PatchMethod = PatchMethod.None;
			Result = PatchResult.Failed;
		}

		public SimplePatchTool SetListener( IListener listener )
		{
			comms.Listener = listener;
			if( IsRunning )
				comms.ListenerCallStarted();

			return this;
		}

		public SimplePatchTool UseRepairPatch( bool canRepairPatch )
		{
			this.canRepairPatch = canRepairPatch;
			return this;
		}

		public SimplePatchTool UseIncrementalPatch( bool canIncrementalPatch )
		{
			this.canIncrementalPatch = canIncrementalPatch;
			return this;
		}

		public SimplePatchTool UseInstallerPatch( bool canInstallerPatch )
		{
			this.canInstallerPatch = canInstallerPatch;
			return this;
		}

		public SimplePatchTool CheckForMultipleRunningInstances( bool checkForMultipleRunningInstances )
		{
			this.checkForMultipleRunningInstances = checkForMultipleRunningInstances;
			return this;
		}

		public SimplePatchTool VerifyFilesOnServer( bool verifyFiles )
		{
			comms.VerifyFiles = verifyFiles;
			return this;
		}

		public SimplePatchTool UseCustomDownloadHandler( DownloadHandlerFactory factoryFunction )
		{
			if( !IsRunning )
			{
				if( factoryFunction == null )
					factoryFunction = () => new CookieAwareWebClient(); // Default WebClient based download handler

				DownloadHandlerFactory = factoryFunction;
				comms.DownloadManager.SetDownloadHandler( factoryFunction() );
			}

			return this;
		}

		public SimplePatchTool UseCustomFreeSpaceCalculator( FreeDiskSpaceCalculator freeSpaceCalculatorFunction )
		{
			if( !IsRunning )
			{
				if( freeSpaceCalculatorFunction == null )
					freeSpaceCalculatorFunction = ( drive ) => new DriveInfo( drive ).AvailableFreeSpace;

				FreeDiskSpaceCalculator = freeSpaceCalculatorFunction;
			}

			return this;
		}

		public SimplePatchTool UseVersionInfoVerifier( XMLVerifier verifierFunction )
		{
			VersionInfoVerifier = verifierFunction;
			return this;
		}

		public SimplePatchTool UsePatchInfoVerifier( XMLVerifier verifierFunction )
		{
			PatchInfoVerifier = verifierFunction;
			return this;
		}

		public SimplePatchTool LogProgress( bool value )
		{
			comms.LogProgress = value;
			return this;
		}

		public SimplePatchTool LogToFile( bool value )
		{
			comms.FileLogging = value;
			return this;
		}

		public SimplePatchTool SilentMode( bool silent )
		{
			comms.SilentMode = silent;
			return this;
		}

		public void Cancel()
		{
			if( IsRunning )
				comms.Cancel = true;
		}

		public string FetchLog()
		{
			return comms.FetchLog();
		}

		public IOperationProgress FetchProgress()
		{
			return comms.FetchProgress();
		}

		public IOperationProgress FetchOverallProgress()
		{
			return comms.FetchOverallProgress();
		}

		public bool CheckForUpdates( bool checkVersionOnly = true )
		{
			if( !IsRunning )
			{
				IsRunning = true;
				Operation = PatchOperation.CheckingForUpdates;
				PatchMethod = PatchMethod.None;
				comms.Cancel = false;

				PatchUtils.CreateBackgroundThread( new ParameterizedThreadStart( ThreadCheckForUpdatesFunction ) ).Start( checkVersionOnly );
				return true;
			}

			return false;
		}

		public bool Run( bool selfPatching )
		{
			if( !IsRunning )
			{
				IsRunning = true;
				Operation = selfPatching ? PatchOperation.SelfPatching : PatchOperation.Patching;
				PatchMethod = PatchMethod.None;
				comms.Cancel = false;

				PatchUtils.CreateBackgroundThread( new ThreadStart( ThreadPatchFunction ) ).Start();
				return true;
			}

			return false;
		}

		// For self-patching applications only - should be called after Run(true) returns PatchResult.Success
		// Starts specified self patcher executable with required parameters
		public bool ApplySelfPatch( string selfPatcherExecutable, string postSelfPatchExecutable = null )
		{
			comms.InitializeFileLogger();
			comms.ListenerCallStarted();

			try
			{
				selfPatcherExecutable = selfPatcherExecutable.Trim();
				if( postSelfPatchExecutable != null )
					postSelfPatchExecutable = postSelfPatchExecutable.Trim();

				if( !File.Exists( selfPatcherExecutable ) )
				{
					comms.Log( Localization.Get( StringId.E_SelfPatcherDoesNotExist ) );
					return false;
				}

				string instructionsPath = comms.CachePath + PatchParameters.SELF_PATCH_INSTRUCTIONS_FILENAME;
				string completedInstructionsPath = comms.CachePath + PatchParameters.SELF_PATCH_COMPLETED_INSTRUCTIONS_FILENAME;
				if( !File.Exists( instructionsPath ) )
					return false;

				FileInfo selfPatcher = new FileInfo( selfPatcherExecutable );

				string args = "\"" + instructionsPath + "\" \"" + completedInstructionsPath + "\"";
				if( !string.IsNullOrEmpty( postSelfPatchExecutable ) && File.Exists( postSelfPatchExecutable ) )
					args += " \"" + postSelfPatchExecutable + "\"";

				ProcessStartInfo startInfo = new ProcessStartInfo( selfPatcher.FullName )
				{
					Arguments = args,
					WorkingDirectory = selfPatcher.DirectoryName
				};

				Process.Start( startInfo );
			}
			catch( Exception e )
			{
				comms.LogToFile( e );
				return false;
			}
			finally
			{
				comms.ListenerCallFinished();
				comms.DisposeFileLogger();
			}

			Process.GetCurrentProcess().Kill();
			return true;
		}

		private void ThreadCheckForUpdatesFunction( object checkVersionOnlyParameter )
		{
			comms.InitializeFileLogger();
			comms.ListenerCallStarted();

			try
			{
				bool checkVersionOnly = (bool) checkVersionOnlyParameter;
				Result = CheckForUpdatesInternal( checkVersionOnly );
			}
			catch( Exception e )
			{
				Result = PatchResult.Failed;
				FailReason = PatchFailReason.FatalException;
				FailDetails = e.ToString();
			}

			if( Result == PatchResult.AlreadyUpToDate )
				comms.Log( Localization.Get( StringId.AppIsUpToDate ) );
			else if( Result == PatchResult.Success )
				comms.Log( Localization.Get( StringId.UpdateAvailable ) );
			else
				comms.Log( comms.FailDetails );

			comms.ListenerCallFinished();
			comms.DisposeFileLogger();

			IsRunning = false;
		}

		private void ThreadPatchFunction()
		{
			comms.InitializeFileLogger();
			comms.ListenerCallStarted();

			try
			{
				Result = Patch();
			}
			catch( Exception e )
			{
				Result = PatchResult.Failed;
				FailReason = PatchFailReason.FatalException;
				FailDetails = e.ToString();
			}

			if( Result == PatchResult.AlreadyUpToDate )
				comms.Log( Localization.Get( StringId.AppIsUpToDate ) );
			else if( Result == PatchResult.Success )
				comms.Log( Operation == PatchOperation.Patching ? Localization.Get( StringId.AppIsUpToDate ) : Localization.Get( StringId.ReadyToSelfPatch ) );
			else
				comms.Log( comms.FailDetails );

			comms.ListenerCallFinished();
			comms.DisposeFileLogger();

			IsRunning = false;
		}

		private PatchResult CheckForUpdatesInternal( bool checkVersionOnly )
		{
			PatchStage = PatchStage.CheckingUpdates;

			comms.Log( Localization.Get( StringId.CheckingForUpdates ) );

			if( !FetchVersionInfo() )
				return PatchResult.Failed;

			if( comms.IsUnderMaintenance() )
				return PatchResult.Failed;

			if( !checkVersionOnly )
			{
				if( CheckLocalFilesUpToDate( true, false ) )
					return PatchResult.AlreadyUpToDate;

				return PatchResult.Success;
			}
			else
			{
				if( currentVersion == comms.VersionInfo.Version )
					return PatchResult.AlreadyUpToDate;
				else
					return PatchResult.Success;
			}
		}

		private PatchResult Patch()
		{
			PatchStage = PatchStage.CheckingUpdates;

			Stopwatch timer = Stopwatch.StartNew();

			comms.Log( Localization.Get( StringId.RetrievingVersionInfo ) );

			if( !FetchVersionInfo() )
				return PatchResult.Failed;

			if( comms.IsUnderMaintenance() )
				return PatchResult.Failed;

			if( !currentVersion.IsValid )
				currentVersion = new VersionCode( 0 );

			VersionCode rootVersion = currentVersion;
			if( comms.SelfPatching )
			{
				VersionCode patchedVersion = PatchUtils.GetVersion( comms.DecompressedFilesPath, comms.VersionInfo.Name );
				if( patchedVersion > currentVersion )
					currentVersion = patchedVersion;
			}

			PatchStage = PatchStage.CheckingFileIntegrity;

			if( CheckLocalFilesUpToDate( true, false ) )
				return PatchResult.AlreadyUpToDate;

			if( !PatchUtils.CheckWriteAccessToFolder( comms.RootPath ) )
			{
				FailReason = PatchFailReason.RequiresAdminPriviledges;
				FailDetails = Localization.Get( StringId.E_AccessToXIsForbiddenRunInAdminMode, comms.RootPath );

				return PatchResult.Failed;
			}

			if( !PatchUtils.CheckWriteAccessToFolder( comms.CachePath ) )
			{
				FailReason = PatchFailReason.RequiresAdminPriviledges;
				FailDetails = Localization.Get( StringId.E_AccessToXIsForbiddenRunInAdminMode, comms.CachePath );

				return PatchResult.Failed;
			}

			if( checkForMultipleRunningInstances )
			{
				string currentExecutablePath = PatchUtils.GetCurrentExecutablePath();
				if( PatchUtils.GetNumberOfRunningProcesses( currentExecutablePath ) > 1 )
				{
					FailReason = PatchFailReason.MultipleRunningInstances;
					FailDetails = Localization.Get( StringId.E_AnotherInstanceOfXIsRunning, Path.GetFileName( currentExecutablePath ) );

					return PatchResult.Failed;
				}
			}

			if( comms.Cancel )
				return PatchResult.Failed;

			// Add a date holder file to the cache to save the last access time reliably
			DateTime dateTimeNow = DateTime.UtcNow;
			File.WriteAllText( comms.CachePath + PatchParameters.CACHE_DATE_HOLDER_FILENAME, dateTimeNow.ToString( "O" ) );

			// Check if there are any leftover files from other SimplePatchTool integrated apps in cache
			DirectoryInfo[] patcherCaches = new DirectoryInfo( comms.CachePath ).Parent.GetDirectories();
			for( int i = 0; i < patcherCaches.Length; i++ )
			{
				DirectoryInfo cacheDir = patcherCaches[i];
				if( cacheDir.Name.Equals( comms.VersionInfo.Name, StringComparison.OrdinalIgnoreCase ) )
					continue;

				FileInfo dateHolder = new FileInfo( PatchUtils.GetPathWithTrailingSeparatorChar( cacheDir.FullName ) + PatchParameters.CACHE_DATE_HOLDER_FILENAME );
				if( dateHolder.Exists && dateHolder.Length > 0L )
				{
					DateTime lastAccessTime;
					if( DateTime.TryParseExact( File.ReadAllText( dateHolder.FullName ), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out lastAccessTime ) )
					{
						if( ( dateTimeNow - lastAccessTime ).TotalDays <= PatchParameters.CACHE_DATE_EXPIRE_DAYS )
							continue;
					}
				}

				// This cache directory doesn't have a date holder file or is older than CACHE_DATE_EXPIRE_DAYS, delete it
				cacheDir.Delete( true );
			}

			bool canRepairPatch = this.canRepairPatch;
			bool canIncrementalPatch = this.canIncrementalPatch;
			bool canInstallerPatch = this.canInstallerPatch;

			List<PatchMethodHolder> preferredPatchMethods = new List<PatchMethodHolder>( 3 );
			List<VersionItem> versionInfoFiles = comms.VersionInfo.Files;

			if( canRepairPatch )
			{
				for( int i = 0; i < versionInfoFiles.Count; i++ )
				{
					VersionItem item = versionInfoFiles[i];
					if( item.CompressedFileSize == 0L && string.IsNullOrEmpty( item.CompressedMd5Hash ) )
					{
						canRepairPatch = false;
						break;
					}
				}

				if( canRepairPatch )
				{
					long repairPatchSize = 0L;
					for( int i = 0; i < versionInfoFiles.Count; i++ )
					{
						VersionItem item = versionInfoFiles[i];
						FileInfo localFile = new FileInfo( comms.RootPath + item.Path );
						if( localFile.Exists && localFile.MatchesSignature( item.FileSize, item.Md5Hash ) )
							continue;

						FileInfo downloadedFile = new FileInfo( comms.DownloadsPath + item.Path );
						if( downloadedFile.Exists && downloadedFile.MatchesSignature( item.CompressedFileSize, item.CompressedMd5Hash ) )
							continue;

						if( comms.SelfPatching )
						{
							FileInfo decompressedFile = new FileInfo( comms.DecompressedFilesPath + item.Path );
							if( decompressedFile.Exists && decompressedFile.MatchesSignature( item.FileSize, item.Md5Hash ) )
								continue;
						}

						repairPatchSize += item.CompressedFileSize;
					}

					preferredPatchMethods.Add( new PatchMethodHolder( PatchMethod.RepairPatch, repairPatchSize ) );
				}
			}

			if( canIncrementalPatch )
			{
				// Find incremental patches to apply
				VersionCode thisVersion = rootVersion;
				List<IncrementalPatch> versionInfoPatches = comms.VersionInfo.IncrementalPatches;
				for( int i = 0; i < versionInfoPatches.Count; i++ )
				{
					if( thisVersion == comms.VersionInfo.Version )
						break;

					IncrementalPatch patch = versionInfoPatches[i];
					if( thisVersion == patch.FromVersion )
					{
						thisVersion = patch.ToVersion;
						incrementalPatches.Add( patch );
					}
				}

				if( thisVersion != comms.VersionInfo.Version )
					incrementalPatches.Clear();

				if( incrementalPatches.Count == 0 )
					canIncrementalPatch = false;
				else
				{
					long incrementalPatchSize = 0L;
					for( int i = 0; i < incrementalPatches.Count; i++ )
					{
						IncrementalPatch incrementalPatch = incrementalPatches[i];
						if( currentVersion > incrementalPatch.FromVersion )
							continue;

						FileInfo patchFile = new FileInfo( comms.GetDownloadPathForPatch( incrementalPatch.PatchVersion() ) );
						if( patchFile.Exists && patchFile.MatchesSignature( incrementalPatch.PatchSize, incrementalPatch.PatchMd5Hash ) )
							continue;

						incrementalPatchSize += incrementalPatch.PatchSize;
					}

					preferredPatchMethods.Add( new PatchMethodHolder( PatchMethod.IncrementalPatch, incrementalPatchSize ) );
				}
			}

			if( canInstallerPatch )
			{
				InstallerPatch installerPatch = comms.VersionInfo.InstallerPatch;
				if( installerPatch.PatchSize == 0L && string.IsNullOrEmpty( installerPatch.PatchMd5Hash ) )
					canInstallerPatch = false;
				else
					preferredPatchMethods.Add( new PatchMethodHolder( PatchMethod.InstallerPatch, installerPatch.PatchSize ) );
			}

			preferredPatchMethods.Sort( ( p1, p2 ) => p1.size.CompareTo( p2.size ) );

			if( preferredPatchMethods.Count == 0 )
			{
				FailReason = PatchFailReason.NoSuitablePatchMethodFound;
				FailDetails = Localization.Get( StringId.E_NoSuitablePatchMethodFound );

				return PatchResult.Failed;
			}

			// Check if there is enough free disk space
			long requiredFreeSpaceInCache = 0L, requiredFreeSpaceInRoot = 0L;
			for( int i = 0; i < versionInfoFiles.Count; i++ )
			{
				VersionItem item = versionInfoFiles[i];
				FileInfo localFile = new FileInfo( comms.RootPath + item.Path );
				if( !localFile.Exists )
				{
					requiredFreeSpaceInCache += item.FileSize;
					requiredFreeSpaceInRoot += item.FileSize;
				}
				else if( !localFile.MatchesSignature( item.FileSize, item.Md5Hash ) )
				{
					requiredFreeSpaceInCache += item.FileSize;

					long deltaSize = item.FileSize - localFile.Length;
					if( deltaSize > 0L )
						requiredFreeSpaceInRoot += deltaSize;
				}
			}

			requiredFreeSpaceInCache += requiredFreeSpaceInCache / 3; // Require additional 33% free space (might be needed by compressed files and/or incremental patches)
			requiredFreeSpaceInCache += 1024 * 1024 * 1024L; // Require additional 1 GB of free space, just in case

			string rootDrive = new DirectoryInfo( comms.RootPath ).Root.FullName;
			string cacheDrive = new DirectoryInfo( comms.CachePath ).Root.FullName;
			if( rootDrive.Equals( cacheDrive, StringComparison.OrdinalIgnoreCase ) )
			{
				if( !CheckFreeSpace( rootDrive, requiredFreeSpaceInCache + requiredFreeSpaceInRoot ) )
					return PatchResult.Failed;
			}
			else
			{
				if( !CheckFreeSpace( rootDrive, requiredFreeSpaceInRoot ) )
					return PatchResult.Failed;

				if( !CheckFreeSpace( cacheDrive, requiredFreeSpaceInCache ) )
					return PatchResult.Failed;
			}

			for( int i = 0; i < preferredPatchMethods.Count; i++ )
				comms.LogToFile( Localization.Get( StringId.PatchMethodXSizeY, preferredPatchMethods[i].method, preferredPatchMethods[i].size.ToMegabytes() + "MB" ) );

			// Start patching
			for( int i = 0; i < preferredPatchMethods.Count; i++ )
			{
				PatchMethod patchMethod = preferredPatchMethods[i].method;

				bool success;
				if( patchMethod == PatchMethod.RepairPatch )
				{
					PatchMethod = PatchMethod.RepairPatch;
					comms.ListenerCallPatchMethodChanged( PatchMethod );

					success = PatchUsingRepairPatch();
				}
				else if( patchMethod == PatchMethod.IncrementalPatch )
				{
					PatchMethod = PatchMethod.IncrementalPatch;
					comms.ListenerCallPatchMethodChanged( PatchMethod );

					success = PatchUsingIncrementalPatches();
				}
				else
				{
					PatchMethod = PatchMethod.InstallerPatch;
					comms.ListenerCallPatchMethodChanged( PatchMethod );

					success = PatchUsingInstallerPatch();
				}

				if( comms.Cancel )
					return PatchResult.Failed;

				if( success )
					break;
				else
				{
					comms.LogToFile( string.Concat( comms.FailReason, ": ", comms.FailDetails ) );

					if( i == preferredPatchMethods.Count - 1 )
						return PatchResult.Failed;
				}
			}

			PatchStage = PatchStage.CheckingFileIntegrity;

			if( !CheckLocalFilesUpToDate( false, comms.SelfPatching ) )
			{
				comms.Log( Localization.Get( StringId.SomeFilesAreStillNotUpToDate ) );

				if( canRepairPatch )
				{
					if( !PatchUsingRepairPatch() )
						return PatchResult.Failed;
				}
				else
				{
					FailReason = PatchFailReason.FilesAreNotUpToDateAfterPatch;
					FailDetails = Localization.Get( StringId.E_FilesAreNotUpToDateAfterPatch );

					return PatchResult.Failed;
				}
			}

			comms.UpdateVersion( comms.VersionInfo.Version );

			PatchStage = PatchStage.DeletingObsoleteFiles;
			comms.Log( Localization.Get( StringId.CalculatingObsoleteFiles ) );

			List<string> obsoleteFiles = FindFilesToDelete( comms.RootPath );
			if( !comms.SelfPatching )
			{
				if( obsoleteFiles.Count > 0 )
				{
					comms.Log( Localization.Get( StringId.DeletingXObsoleteFiles, obsoleteFiles.Count ) );
					for( int i = 0; i < obsoleteFiles.Count; i++ )
					{
						comms.Log( Localization.Get( StringId.DeletingX, obsoleteFiles[i] ) );
						File.Delete( comms.RootPath + obsoleteFiles[i] );
					}
				}
				else
					comms.Log( Localization.Get( StringId.NoObsoleteFiles ) );

				PatchUtils.DeleteDirectory( comms.CachePath );
			}
			else
			{
				// Delete obsolete self patching files
				List<string> obsoleteSelfPatchingFiles = FindFilesToDelete( comms.DecompressedFilesPath );
				if( obsoleteSelfPatchingFiles.Count > 0 )
				{
					comms.Log( Localization.Get( StringId.DeletingXObsoleteFiles, obsoleteSelfPatchingFiles.Count ) );
					for( int i = 0; i < obsoleteSelfPatchingFiles.Count; i++ )
					{
						comms.Log( Localization.Get( StringId.DeletingX, obsoleteSelfPatchingFiles[i] ) );
						File.Delete( comms.DecompressedFilesPath + obsoleteSelfPatchingFiles[i] );
					}
				}
				else
					comms.Log( Localization.Get( StringId.NoObsoleteFiles ) );

				// Self patcher executable, if exists, can't self patch itself, so patch it manually here
				// This assumes that self patcher and any related files are located at SELF_PATCHER_DIRECTORY
				string selfPatcherFiles = comms.DecompressedFilesPath + PatchParameters.SELF_PATCHER_DIRECTORY;
				if( Directory.Exists( selfPatcherFiles ) )
					PatchUtils.MoveDirectory( selfPatcherFiles, comms.RootPath + PatchParameters.SELF_PATCHER_DIRECTORY );

				string separator = PatchParameters.SELF_PATCH_OP_SEPARATOR;
				StringBuilder sb = new StringBuilder( 500 );

				// Append current version to the beginning of the file
				sb.Append( rootVersion );

				// 1. Rename files
				if( incrementalPatchesInfo.Count > 0 )
				{
					sb.Append( separator ).Append( PatchParameters.SELF_PATCH_MOVE_OP );
					for( int i = 0; i < incrementalPatchesInfo.Count; i++ )
					{
						IncrementalPatchInfo incrementalPatch = incrementalPatchesInfo[i];
						for( int j = 0; j < incrementalPatch.RenamedFiles.Count; j++ )
						{
							PatchRenamedItem renamedItem = incrementalPatch.RenamedFiles[j];
							sb.Append( separator ).Append( comms.RootPath + renamedItem.BeforePath ).Append( separator ).Append( comms.RootPath + renamedItem.AfterPath );
						}
					}
				}

				// 2. Update files
				sb.Append( separator ).Append( PatchParameters.SELF_PATCH_MOVE_OP );

				DirectoryInfo updatedFilesDir = new DirectoryInfo( comms.DecompressedFilesPath );
				DirectoryInfo[] updatedSubDirectories = updatedFilesDir.GetDirectories();
				for( int i = 0; i < updatedSubDirectories.Length; i++ )
					sb.Append( separator ).Append( comms.DecompressedFilesPath ).Append( updatedSubDirectories[i].Name ).Append( Path.DirectorySeparatorChar ).Append( separator ).Append( comms.RootPath ).Append( updatedSubDirectories[i].Name ).Append( Path.DirectorySeparatorChar );

				string versionHolderFilename = comms.VersionInfo.Name + PatchParameters.VERSION_HOLDER_FILENAME_POSTFIX;
				FileInfo[] updatedFiles = updatedFilesDir.GetFiles();
				for( int i = 0; i < updatedFiles.Length; i++ )
				{
					// Don't update the version holder file until everything else is updated properly
					if( updatedFiles[i].Name != versionHolderFilename )
						sb.Append( separator ).Append( comms.DecompressedFilesPath ).Append( updatedFiles[i].Name ).Append( separator ).Append( comms.RootPath ).Append( updatedFiles[i].Name );
				}

				// Update the version holder now
				sb.Append( separator ).Append( comms.DecompressedFilesPath ).Append( versionHolderFilename ).Append( separator ).Append( comms.RootPath ).Append( versionHolderFilename );

				// 3. Delete obsolete files
				if( obsoleteFiles.Count > 0 )
				{
					string selfPatcherDirectory = PatchParameters.SELF_PATCHER_DIRECTORY + Path.DirectorySeparatorChar;
					sb.Append( separator ).Append( PatchParameters.SELF_PATCH_DELETE_OP );

					comms.Log( Localization.Get( StringId.DeletingXObsoleteFiles, obsoleteFiles.Count ) );
					for( int i = 0; i < obsoleteFiles.Count; i++ )
					{
						// Delete the obsolete files inside SELF_PATCHER_DIRECTORY manually
						string absolutePath = comms.RootPath + obsoleteFiles[i];
						if( obsoleteFiles[i].StartsWith( selfPatcherDirectory, StringComparison.OrdinalIgnoreCase ) )
						{
							comms.Log( Localization.Get( StringId.DeletingX, obsoleteFiles[i] ) );

							if( File.Exists( absolutePath ) )
								File.Delete( absolutePath );
							else if( Directory.Exists( absolutePath ) )
								PatchUtils.DeleteDirectory( absolutePath );
						}
						else
						{
							// '-->' indicates that the file will be deleted by the self patcher executable
							comms.LogToFile( Localization.Get( StringId.DeletingX, "--> " + obsoleteFiles[i] ) );
							sb.Append( separator ).Append( absolutePath );
						}
					}
				}
				else
					comms.Log( Localization.Get( StringId.NoObsoleteFiles ) );

				sb.Append( separator ).Append( comms.CachePath );

				File.Delete( comms.CachePath + PatchParameters.SELF_PATCH_COMPLETED_INSTRUCTIONS_FILENAME );
				File.WriteAllText( comms.CachePath + PatchParameters.SELF_PATCH_INSTRUCTIONS_FILENAME, sb.Append( separator ).ToString() );
			}

			comms.Log( Localization.Get( StringId.PatchCompletedInXSeconds, timer.ElapsedSeconds() ) );
			return PatchResult.Success;
		}

		private bool FetchVersionInfo()
		{
			string versionInfoXML = comms.DownloadManager.DownloadTextFromURL( versionInfoURL );
			if( string.IsNullOrEmpty( versionInfoXML ) )
			{
				FailReason = PatchFailReason.DownloadError;
				FailDetails = Localization.Get( StringId.E_VersionInfoCouldNotBeDownloaded );

				return false;
			}

			if( VersionInfoVerifier != null && !VersionInfoVerifier( ref versionInfoXML ) )
			{
				FailReason = PatchFailReason.CantVerifyVersionInfo;
				FailDetails = Localization.Get( StringId.E_VersionInfoCouldNotBeVerified );

				return false;
			}

			try
			{
				comms.VersionInfo = PatchUtils.DeserializeXMLToVersionInfo( versionInfoXML );
			}
			catch( Exception e )
			{
				comms.LogToFile( e );

				FailReason = PatchFailReason.XmlDeserializeError;
				FailDetails = Localization.Get( StringId.E_VersionInfoInvalid );

				return false;
			}

			if( comms.Cancel )
				return false;

			if( !comms.VersionInfo.Version.IsValid )
			{
				FailReason = PatchFailReason.InvalidVersionCode;
				FailDetails = Localization.Get( StringId.E_VersionInfoInvalid );

				return false;
			}

			incrementalPatches.Clear();
			incrementalPatchesInfo.Clear();
			filesInVersion.Clear();

			comms.ListenerCallVersionInfoFetched( comms.VersionInfo );

			List<VersionItem> versionInfoFiles = comms.VersionInfo.Files;
			for( int i = 0; i < versionInfoFiles.Count; i++ )
				filesInVersion.Add( versionInfoFiles[i].Path );

			currentVersion = PatchUtils.GetVersion( comms.RootPath, comms.VersionInfo.Name );
			comms.ListenerCallVersionFetched( currentVersion, comms.VersionInfo.Version );

			return true;
		}

		private bool PatchUsingRepairPatch()
		{
			if( comms.Cancel )
				return false;

			comms.LogToFile( Localization.Get( StringId.ApplyingRepairPatch ) );

			if( new RepairPatchApplier( comms ).Run() == PatchResult.Failed )
				return false;

			return true;
		}

		private bool PatchUsingIncrementalPatches()
		{
			if( comms.Cancel )
				return false;

			comms.LogToFile( Localization.Get( StringId.ApplyingIncrementalPatch ) );

			if( incrementalPatches.Count == 0 )
				return false;

			if( comms.VerifyFiles )
			{
				PatchStage = PatchStage.VerifyingFilesOnServer;

				for( int i = 0; i < incrementalPatches.Count; i++ )
				{
					if( comms.Cancel )
						return false;

					IncrementalPatch incrementalPatch = incrementalPatches[i];
					long fileSize;
					if( !comms.DownloadManager.FileExistsAtUrl( comms.VersionInfo.GetInfoURLFor( incrementalPatch ), out fileSize ) )
					{
						FailReason = PatchFailReason.FileDoesNotExistOnServer;
						FailDetails = Localization.Get( StringId.E_FileXDoesNotExistOnServer, incrementalPatch.PatchVersion() + PatchParameters.INCREMENTAL_PATCH_INFO_EXTENSION );

						return false;
					}

					if( incrementalPatch.Files > 0 )
					{
						if( !comms.DownloadManager.FileExistsAtUrl( comms.VersionInfo.GetDownloadURLFor( incrementalPatch ), out fileSize ) )
						{
							FailReason = PatchFailReason.FileDoesNotExistOnServer;
							FailDetails = Localization.Get( StringId.E_FileXDoesNotExistOnServer, incrementalPatch.PatchVersion() + PatchParameters.INCREMENTAL_PATCH_FILE_EXTENSION );

							return false;
						}
						else if( fileSize > 0L && fileSize != incrementalPatch.PatchSize )
						{
							FailReason = PatchFailReason.FileIsNotValidOnServer;
							FailDetails = Localization.Get( StringId.E_FileXIsNotValidOnServer, incrementalPatch.PatchVersion() + PatchParameters.INCREMENTAL_PATCH_FILE_EXTENSION );

							return false;
						}
					}
				}
			}

			for( int i = 0; i < incrementalPatches.Count; i++ )
			{
				if( comms.Cancel )
					return false;

				IncrementalPatch incrementalPatch = incrementalPatches[i];
				string patchInfoXML = comms.DownloadManager.DownloadTextFromURL( comms.VersionInfo.GetInfoURLFor( incrementalPatch ) );
				if( patchInfoXML == null )
				{
					FailReason = PatchFailReason.DownloadError;
					FailDetails = Localization.Get( StringId.E_CouldNotDownloadPatchInfoX, incrementalPatch.PatchVersionBrief() );

					return false;
				}

				if( PatchInfoVerifier != null && !PatchInfoVerifier( ref patchInfoXML ) )
				{
					FailReason = PatchFailReason.CantVerifyPatchInfo;
					FailDetails = Localization.Get( StringId.E_PatchInfoCouldNotBeVerified );

					return false;
				}

				IncrementalPatchInfo patchInfo;
				try
				{
					patchInfo = PatchUtils.DeserializeXMLToIncrementalPatchInfo( patchInfoXML );
				}
				catch( Exception e )
				{
					comms.LogToFile( e );

					FailReason = PatchFailReason.XmlDeserializeError;
					FailDetails = Localization.Get( StringId.E_InvalidPatchInfoX, incrementalPatch.PatchVersionBrief() );

					return false;
				}

				patchInfo.FromVersion = incrementalPatch.FromVersion;
				patchInfo.ToVersion = incrementalPatch.ToVersion;
				patchInfo.DownloadURL = comms.VersionInfo.GetDownloadURLFor( incrementalPatch );
				patchInfo.CompressedFileSize = incrementalPatch.PatchSize;
				patchInfo.CompressedMd5Hash = incrementalPatch.PatchMd5Hash;
				patchInfo.CompressionFormat = incrementalPatch.CompressionFormat;

				incrementalPatchesInfo.Add( patchInfo );

				if( currentVersion > incrementalPatch.FromVersion )
					continue;

				if( new IncrementalPatchApplier( comms, patchInfo ).Run() == PatchResult.Failed )
					return false;
			}

			return true;
		}

		private bool PatchUsingInstallerPatch()
		{
			if( comms.Cancel )
				return false;

			comms.LogToFile( Localization.Get( StringId.ApplyingInstallerPatch ) );

			if( comms.VerifyFiles )
			{
				PatchStage = PatchStage.VerifyingFilesOnServer;

				InstallerPatch installerPatch = comms.VersionInfo.InstallerPatch;
				long fileSize;
				if( !comms.DownloadManager.FileExistsAtUrl( comms.VersionInfo.GetDownloadURLFor( installerPatch ), out fileSize ) )
				{
					FailReason = PatchFailReason.FileDoesNotExistOnServer;
					FailDetails = Localization.Get( StringId.E_FileXDoesNotExistOnServer, PatchParameters.INSTALLER_PATCH_FILENAME );

					return false;
				}
				else if( fileSize > 0L && fileSize != installerPatch.PatchSize )
				{
					FailReason = PatchFailReason.FileIsNotValidOnServer;
					FailDetails = Localization.Get( StringId.E_FileXIsNotValidOnServer, PatchParameters.INSTALLER_PATCH_FILENAME );

					return false;
				}
			}

			if( new InstallerPatchApplier( comms ).Run() == PatchResult.Failed )
				return false;

			return true;
		}

		private bool CheckFreeSpace( string drive, long requiredFreeSpace )
		{
			if( FreeDiskSpaceCalculator( drive ) < requiredFreeSpace )
			{
				FailReason = PatchFailReason.InsufficientSpace;
				FailDetails = Localization.Get( StringId.E_InsufficientSpaceXNeededInY, requiredFreeSpace.ToMegabytes() + "MB", drive );

				return false;
			}

			return true;
		}

		private bool CheckLocalFilesUpToDate( bool checkObsoleteFiles, bool searchSelfPatchFiles )
		{
			comms.Log( Localization.Get( StringId.CheckingIfFilesAreUpToDate ) );

			List<VersionItem> versionInfoFiles = comms.VersionInfo.Files;
			for( int i = 0; i < versionInfoFiles.Count; i++ )
			{
				VersionItem item = versionInfoFiles[i];
				FileInfo localFile = new FileInfo( comms.RootPath + item.Path );
				if( !localFile.Exists || !localFile.MatchesSignature( item.FileSize, item.Md5Hash ) )
				{
					if( searchSelfPatchFiles )
					{
						FileInfo decompressedFile = new FileInfo( comms.DecompressedFilesPath + item.Path );
						if( decompressedFile.Exists && decompressedFile.MatchesSignature( item.FileSize, item.Md5Hash ) )
							continue;
					}

					return false;
				}
			}

			// Check if there are any obsolete files
			return !checkObsoleteFiles || FindFilesToDelete( comms.RootPath ).Count == 0;
		}

		private List<string> FindFilesToDelete( string rootPath )
		{
			List<string> filesToDelete = new List<string>();
			FindFilesToDelete( rootPath, filesToDelete );
			return filesToDelete;
		}

		private void FindFilesToDelete( string rootPath, List<string> filesToDelete, string relativePath = "" )
		{
			DirectoryInfo directory = new DirectoryInfo( rootPath + relativePath );

			FileInfo[] files = directory.GetFiles();
			for( int i = 0; i < files.Length; i++ )
			{
				string fileRelativePath = relativePath + files[i].Name;
				if( !filesInVersion.Contains( fileRelativePath ) && !comms.VersionInfo.IgnoredPathsRegex.PathMatchesPattern( fileRelativePath ) )
					filesToDelete.Add( fileRelativePath );
			}

			DirectoryInfo[] subDirectories = directory.GetDirectories();
			for( int i = 0; i < subDirectories.Length; i++ )
			{
				string directoryRelativePath = relativePath + subDirectories[i].Name + Path.DirectorySeparatorChar;
				if( !comms.VersionInfo.IgnoredPathsRegex.PathMatchesPattern( directoryRelativePath ) )
					FindFilesToDelete( rootPath, filesToDelete, directoryRelativePath );
			}
		}
	}
}