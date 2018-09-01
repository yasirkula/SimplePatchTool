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
		InsufficientSpace, RequiresAdminPriviledges,
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
		private enum PatchMethod { None, IncrementalPatch, Repair }

		private readonly PatchIntercomms comms;
		private readonly string versionInfoURL;

		private VersionCode currentVersion;

		private bool canIncrementalPatch;
		private bool canRepair;

		private readonly List<IncrementalPatch> incrementalPatches;
		private readonly List<PatchInfo> incrementalPatchesInfo;
		private readonly HashSet<string> filesInVersion;

		public bool IsRunning { get; private set; }
		public PatchOperation Operation { get; private set; }
		public PatchResult Result { get; private set; }

		internal DownloadHandlerFactory DownloadHandlerFactory { get; private set; }
		internal FreeDiskSpaceCalculator FreeDiskSpaceCalculator { get; private set; }
		internal XMLVerifier VersionInfoVerifier { get; private set; }
		internal XMLVerifier PatchInfoVerifier { get; private set; }

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

			comms = new PatchIntercomms( this, PatchUtils.GetPathWithTrailingSeparatorChar( rootPath ) );
			this.versionInfoURL = versionInfoURL;

			canIncrementalPatch = true;
			canRepair = true;

			incrementalPatches = new List<IncrementalPatch>();
			incrementalPatchesInfo = new List<PatchInfo>();
			filesInVersion = new HashSet<string>();

			UseCustomDownloadHandler( null );
			UseCustomFreeSpaceCalculator( null );

			IsRunning = false;
			Result = PatchResult.Failed;
		}

		public SimplePatchTool UseIncrementalPatch( bool canIncrementalPatch )
		{
			this.canIncrementalPatch = canIncrementalPatch;
			return this;
		}

		public SimplePatchTool UseRepair( bool canRepair )
		{
			this.canRepair = canRepair;
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
			comms.LogToFile = value;
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

		public bool CheckForUpdates( bool checkVersionOnly = true )
		{
			if( !IsRunning )
			{
				IsRunning = true;
				Operation = PatchOperation.CheckingForUpdates;
				comms.Cancel = false;

				Thread workerThread = new Thread( new ParameterizedThreadStart( ThreadCheckForUpdatesFunction ) ) { IsBackground = true };
				workerThread.Start( checkVersionOnly );

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
				comms.Cancel = false;

				Thread workerThread = new Thread( new ThreadStart( ThreadPatchFunction ) ) { IsBackground = true };
				workerThread.Start();

				return true;
			}

			return false;
		}

		// For self-patching applications only - should be called after Run(true) returns PatchResult.Success
		// Starts specified self patcher executable with required parameters
		public bool ApplySelfPatch( string selfPatcherExecutable, string postSelfPatchExecutable = null )
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
			Process.GetCurrentProcess().Kill();

			return true;
		}

		private void ThreadCheckForUpdatesFunction( object checkVersionOnlyParameter )
		{
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

			comms.DisposeFileLogger();
			IsRunning = false;
		}

		private void ThreadPatchFunction()
		{
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

			comms.DisposeFileLogger();
			IsRunning = false;
		}

		private PatchResult CheckForUpdatesInternal( bool checkVersionOnly )
		{
			PatchStage = PatchStage.CheckingUpdates;

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
				if( currentVersion >= comms.VersionInfo.Version )
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

			if( comms.SelfPatching )
			{
				// If a previous post self-patch operation was not finished, finish it first
				FileInfo awaitingInstructions = new FileInfo( comms.CachePath + PatchParameters.SELF_PATCH_INSTRUCTIONS_FILENAME );
				if( awaitingInstructions.Exists && awaitingInstructions.Length > 0L )
				{
					string instructions = File.ReadAllText( awaitingInstructions.FullName );
					int versionEndIndex = instructions.IndexOf( PatchParameters.SELF_PATCH_OP_SEPARATOR );
					if( versionEndIndex > 0 && instructions.Substring( 0, versionEndIndex ) == rootVersion )
						return PatchResult.Success;

					// Instructions may have become obsolete, delete them
					awaitingInstructions.Delete();
				}
			}

			PatchMethod patchMethod = PatchMethod.None;

			// Check if repair patch exists
			List<VersionItem> versionInfoFiles = comms.VersionInfo.Files;
			for( int i = 0; i < versionInfoFiles.Count; i++ )
			{
				// Files will have default compressed values when repair patch is not generated
				VersionItem item = versionInfoFiles[i];
				if( item.CompressedFileSize == 0L && string.IsNullOrEmpty( item.CompressedMd5Hash ) )
				{
					canRepair = false;
					break;
				}
			}

			// Find patch method to use by default
			if( canIncrementalPatch )
			{
				// Find incremental patches to apply
				VersionCode thisVersion = rootVersion;
				List<IncrementalPatch> versionInfoPatches = comms.VersionInfo.Patches;
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
					patchMethod = canRepair ? PatchMethod.Repair : PatchMethod.None;
				else if( !canRepair )
					patchMethod = PatchMethod.IncrementalPatch;
				else
				{
					// Find cheapest patch method
					long incrementalPatchSize = 0L;
					long repairSize = 0L;
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

					for( int i = 0; i < versionInfoFiles.Count; i++ )
					{
						VersionItem item = versionInfoFiles[i];

						FileInfo localFile = new FileInfo( comms.RootPath + item.Path );
						if( localFile.Exists && localFile.MatchesSignature( item.FileSize, item.Md5Hash ) )
							continue;

						FileInfo downloadedFile = new FileInfo( comms.DownloadsPath + item.Path );
						if( downloadedFile.Exists && downloadedFile.MatchesSignature( item.FileSize, item.Md5Hash ) )
							continue;

						if( comms.SelfPatching )
						{
							FileInfo decompressedFile = new FileInfo( comms.DecompressedFilesPath + item.Path );
							if( decompressedFile.Exists && decompressedFile.MatchesSignature( item.FileSize, item.Md5Hash ) )
								continue;
						}

						repairSize += item.CompressedFileSize;
					}

					patchMethod = repairSize <= incrementalPatchSize ? PatchMethod.Repair : PatchMethod.IncrementalPatch;
				}
			}
			else if( canRepair )
				patchMethod = PatchMethod.Repair;

			if( patchMethod == PatchMethod.None )
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

			// Start patching
			if( patchMethod == PatchMethod.Repair )
			{
				if( !PatchUsingRepair() )
				{
					if( comms.Cancel )
						return PatchResult.Failed;

					if( !canIncrementalPatch || !PatchUsingIncrementalPatches() )
						return PatchResult.Failed;
				}
			}
			else if( !PatchUsingIncrementalPatches() )
			{
				if( comms.Cancel )
					return PatchResult.Failed;

				if( canRepair )
				{
					canRepair = false;

					if( !PatchUsingRepair() )
						return PatchResult.Failed;
				}
				else
					return PatchResult.Failed;
			}

			PatchStage = PatchStage.CheckingFileIntegrity;

			if( !CheckLocalFilesUpToDate( false, comms.SelfPatching ) )
			{
				if( patchMethod == PatchMethod.IncrementalPatch && canRepair )
				{
					comms.Log( Localization.Get( StringId.SomeFilesAreStillNotUpToDate ) );

					if( !PatchUsingRepair() )
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

				comms.DisposeFileLogger(); // Can't delete CachePath while a StreamWriter is still open inside

				if( Directory.Exists( comms.CachePath ) )
					Directory.Delete( comms.CachePath, true );
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

				// Rename files
				if( incrementalPatchesInfo.Count > 0 )
				{
					sb.Append( separator ).Append( PatchParameters.SELF_PATCH_MOVE_OP );
					for( int i = 0; i < incrementalPatchesInfo.Count; i++ )
					{
						PatchInfo incrementalPatch = incrementalPatchesInfo[i];
						for( int j = 0; j < incrementalPatch.RenamedFiles.Count; j++ )
						{
							PatchRenamedItem renamedItem = incrementalPatch.RenamedFiles[j];
							sb.Append( separator ).Append( comms.RootPath + renamedItem.BeforePath ).Append( separator ).Append( comms.RootPath + renamedItem.AfterPath );
						}
					}
				}

				// Update files
				sb.Append( separator ).Append( PatchParameters.SELF_PATCH_MOVE_OP ).Append( separator ).Append( comms.DecompressedFilesPath ).Append( separator ).Append( comms.RootPath );

				// Delete obsolete files
				sb.Append( separator ).Append( PatchParameters.SELF_PATCH_DELETE_OP );
				for( int i = 0; i < obsoleteFiles.Count; i++ )
					sb.Append( separator ).Append( comms.RootPath + obsoleteFiles[i] );

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
			if( versionInfoXML == null )
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
			catch
			{
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

			List<VersionItem> versionInfoFiles = comms.VersionInfo.Files;
			for( int i = 0; i < versionInfoFiles.Count; i++ )
				filesInVersion.Add( versionInfoFiles[i].Path );

			currentVersion = PatchUtils.GetVersion( comms.RootPath, comms.VersionInfo.Name );
			return true;
		}

		private bool PatchUsingRepair()
		{
			if( comms.Cancel )
				return false;

			PatchResult repairResult = new RepairApplier( comms ).Run();
			if( repairResult == PatchResult.Failed )
				return false;

			return true;
		}

		private bool PatchUsingIncrementalPatches()
		{
			if( comms.Cancel )
				return false;

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
						FailDetails = Localization.Get( StringId.E_FileXDoesNotExistOnServer, incrementalPatch.PatchVersion() + PatchParameters.PATCH_INFO_EXTENSION );

						return false;
					}

					if( !comms.DownloadManager.FileExistsAtUrl( comms.VersionInfo.GetDownloadURLFor( incrementalPatch ), out fileSize ) )
					{
						FailReason = PatchFailReason.FileDoesNotExistOnServer;
						FailDetails = Localization.Get( StringId.E_FileXDoesNotExistOnServer, incrementalPatch.PatchVersion() + PatchParameters.PATCH_FILE_EXTENSION );

						return false;
					}
					else if( fileSize > 0L && fileSize != incrementalPatch.PatchSize )
					{
						FailReason = PatchFailReason.FileIsNotValidOnServer;
						FailDetails = Localization.Get( StringId.E_FileXIsNotValidOnServer, incrementalPatch.PatchVersion() + PatchParameters.PATCH_FILE_EXTENSION );

						return false;
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

				PatchInfo patchInfo;
				try
				{
					patchInfo = PatchUtils.DeserializeXMLToPatchInfo( patchInfoXML );
				}
				catch
				{
					FailReason = PatchFailReason.XmlDeserializeError;
					FailDetails = Localization.Get( StringId.E_InvalidPatchInfoX, incrementalPatch.PatchVersionBrief() );

					return false;
				}

				patchInfo.FromVersion = incrementalPatch.FromVersion;
				patchInfo.ToVersion = incrementalPatch.ToVersion;
				patchInfo.DownloadURL = comms.VersionInfo.GetDownloadURLFor( incrementalPatch );
				patchInfo.CompressedFileSize = incrementalPatch.PatchSize;
				patchInfo.CompressedMd5Hash = incrementalPatch.PatchMd5Hash;

				incrementalPatchesInfo.Add( patchInfo );

				if( currentVersion > incrementalPatch.FromVersion )
					continue;

				PatchResult patchResult = new IncrementalPatchApplier( comms, patchInfo ).Run();
				if( patchResult == PatchResult.Failed )
					return false;
			}

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

		private bool CheckLocalFilesUpToDate( bool checkDeletedFiles, bool searchSelfPatchFiles )
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
			return !checkDeletedFiles || FindFilesToDelete( comms.RootPath ).Count == 0;
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
				FindFilesToDelete( rootPath, filesToDelete, directoryRelativePath );
			}
		}
	}
}