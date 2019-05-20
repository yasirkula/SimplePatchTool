using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SimplePatchToolCore
{
	public class PatchCreator
	{
		private VersionInfo versionInfo;
		private IncrementalPatchInfo incrementalPatch;

		private readonly string rootPath;
		private readonly string outputPath;
		private readonly string repairPatchOutputPath;
		private readonly string installerPatchOutputPath;
		private readonly string incrementalPatchOutputPath;
		private readonly string incrementalPatchTempPath;
		private readonly string projectName;

		private string previousVersionRoot;

		private VersionCode previousVersion;
		private readonly VersionCode version;

		private bool generateRepairPatch;
		private bool generateInstallerPatch;
		private bool GenerateIncrementalPatch { get { return !string.IsNullOrEmpty( previousVersionRoot ); } }

		private HashSet<string> ignoredPaths;
		private List<Regex> ignoredPathsRegex;

		private CompressionFormat compressionFormat;

		private string baseDownloadURL;
		private string maintenanceCheckURL;

		private Queue<string> logs;

		private bool cancel;
		private bool silentMode;

		public bool IsRunning { get; private set; }
		public PatchResult Result { get; private set; }

		/// <exception cref = "DirectoryNotFoundException">Root path does not exist</exception>
		/// <exception cref = "UnauthorizedAccessException">A path needs admin priviledges to write</exception>
		/// <exception cref = "IOException">Output path is not empty</exception>
		/// <exception cref = "ArgumentException">An argument is empty</exception>
		/// <exception cref = "FormatException">Version is invalid</exception>
		public PatchCreator( string rootPath, string outputPath, string projectName, VersionCode version )
		{
			rootPath = rootPath.Trim();
			outputPath = outputPath.Trim();
			projectName = projectName.Trim();

			if( string.IsNullOrEmpty( rootPath ) )
				throw new ArgumentException( Localization.Get( StringId.E_XCanNotBeEmpty, "'rootPath'" ) );

			if( string.IsNullOrEmpty( outputPath ) )
				throw new ArgumentException( Localization.Get( StringId.E_XCanNotBeEmpty, "'outputPath'" ) );

			if( string.IsNullOrEmpty( projectName ) )
				throw new ArgumentException( Localization.Get( StringId.E_XCanNotBeEmpty, "'projectName'" ) );

			if( !version.IsValid )
				throw new FormatException( Localization.Get( StringId.E_VersionCodeXIsInvalid, version ) );

			if( !Directory.Exists( rootPath ) )
				throw new DirectoryNotFoundException( Localization.Get( StringId.E_XDoesNotExist, rootPath ) );

			if( !PatchUtils.CheckWriteAccessToFolder( rootPath ) )
				throw new UnauthorizedAccessException( Localization.Get( StringId.E_AccessToXIsForbiddenRunInAdminMode, rootPath ) );

			if( !PatchUtils.CheckWriteAccessToFolder( outputPath ) )
				throw new UnauthorizedAccessException( Localization.Get( StringId.E_AccessToXIsForbiddenRunInAdminMode, outputPath ) );

			if( Directory.Exists( outputPath ) )
			{
				if( Directory.GetFiles( outputPath ).Length > 0 || Directory.GetDirectories( outputPath ).Length > 0 )
					throw new IOException( Localization.Get( StringId.E_DirectoryXIsNotEmpty, outputPath ) );
			}

			Localization.Get( StringId.Done ); // Force the localization system to be initialized with the current culture/language

			generateRepairPatch = true;
			generateInstallerPatch = true;
			previousVersionRoot = null;

			this.previousVersion = null;
			this.version = version;

			this.rootPath = PatchUtils.GetPathWithTrailingSeparatorChar( rootPath );
			this.outputPath = PatchUtils.GetPathWithTrailingSeparatorChar( outputPath );
			this.projectName = projectName;

			repairPatchOutputPath = this.outputPath + PatchParameters.REPAIR_PATCH_DIRECTORY + Path.DirectorySeparatorChar;
			installerPatchOutputPath = this.outputPath + PatchParameters.INSTALLER_PATCH_DIRECTORY + Path.DirectorySeparatorChar;
			incrementalPatchOutputPath = this.outputPath + PatchParameters.INCREMENTAL_PATCH_DIRECTORY + Path.DirectorySeparatorChar;
			incrementalPatchTempPath = this.outputPath + "Temp" + Path.DirectorySeparatorChar;

			ignoredPaths = new HashSet<string>();
			ignoredPathsRegex = new List<Regex>() { PatchUtils.WildcardToRegex( "*" + PatchParameters.VERSION_HOLDER_FILENAME_POSTFIX ) }; // Ignore any version holder files

			compressionFormat = CompressionFormat.LZMA;

			baseDownloadURL = "";
			maintenanceCheckURL = "";

			logs = new Queue<string>();

			cancel = false;
			silentMode = false;

			IsRunning = false;
			Result = PatchResult.Failed;
		}

		public PatchCreator LoadIgnoredPathsFromFile( string pathToIgnoredPathsList )
		{
			if( !string.IsNullOrEmpty( pathToIgnoredPathsList ) )
			{
				string[] ignoredPaths = File.ReadAllLines( pathToIgnoredPathsList.Trim() );
				AddIgnoredPaths( ignoredPaths );
			}

			return this;
		}

		public PatchCreator AddIgnoredPath( string ignoredPath )
		{
			if( ignoredPath != null )
				ignoredPath = ignoredPath.Trim().Replace( PatchUtils.AltDirectorySeparatorChar, Path.DirectorySeparatorChar );

			if( !string.IsNullOrEmpty( ignoredPath ) )
			{
				if( ignoredPaths.Add( ignoredPath ) )
					ignoredPathsRegex.Add( PatchUtils.WildcardToRegex( ignoredPath ) );
			}

			return this;
		}

		public PatchCreator AddIgnoredPaths( IEnumerable<string> ignoredPaths )
		{
			if( ignoredPaths != null )
			{
				foreach( string ignoredPath in ignoredPaths )
					AddIgnoredPath( ignoredPath );
			}

			return this;
		}

		public PatchCreator CreateRepairPatch( bool value )
		{
			generateRepairPatch = value;
			return this;
		}

		public PatchCreator CreateInstallerPatch( bool value )
		{
			generateInstallerPatch = value;
			return this;
		}

		/// <exception cref = "DirectoryNotFoundException">Previous version's path does not exist</exception>
		/// <exception cref = "ArgumentException">previousVersionRoot is empty</exception>
		/// <exception cref = "FileNotFoundException">Previous version's version code does not exist</exception>
		/// <exception cref = "FormatException">Previous version's version code is not valid</exception>
		/// <exception cref = "InvalidOperationException">Previous version's version code is greater than or equal to current version's version code</exception>
		public PatchCreator CreateIncrementalPatch( bool value, string previousVersionRoot = null )
		{
			if( !value || previousVersionRoot == null )
				this.previousVersionRoot = null;
			else
			{
				previousVersionRoot = previousVersionRoot.Trim();
				if( string.IsNullOrEmpty( previousVersionRoot ) )
					throw new ArgumentException( Localization.Get( StringId.E_XCanNotBeEmpty, "'previousVersionRoot'" ) );

				previousVersionRoot = PatchUtils.GetPathWithTrailingSeparatorChar( previousVersionRoot );

				if( !Directory.Exists( previousVersionRoot ) )
					throw new DirectoryNotFoundException( Localization.Get( StringId.E_XDoesNotExist, previousVersionRoot ) );

				previousVersion = PatchUtils.GetVersion( previousVersionRoot, projectName );
				if( previousVersion == null )
					throw new FileNotFoundException( Localization.Get( StringId.E_XDoesNotExist, projectName + PatchParameters.VERSION_HOLDER_FILENAME_POSTFIX ) );
				else if( !previousVersion.IsValid )
					throw new FormatException( Localization.Get( StringId.E_VersionCodeXIsInvalid, previousVersion ) );

				if( previousVersion >= version )
					throw new InvalidOperationException( Localization.Get( StringId.E_PreviousVersionXIsNotLessThanY, previousVersion, version ) );

				this.previousVersionRoot = previousVersionRoot;
			}

			return this;
		}

		public PatchCreator SetBaseDownloadURL( string baseDownloadURL )
		{
			if( baseDownloadURL == null )
				baseDownloadURL = "";

			this.baseDownloadURL = baseDownloadURL.Trim();
			return this;
		}

		public PatchCreator SetMaintenanceCheckURL( string maintenanceCheckURL )
		{
			if( maintenanceCheckURL == null )
				maintenanceCheckURL = "";

			this.maintenanceCheckURL = maintenanceCheckURL;
			return this;
		}

		public PatchCreator SetCompressionFormat( CompressionFormat compressionFormat )
		{
			if( !IsRunning )
				this.compressionFormat = compressionFormat;

			return this;
		}

		public PatchCreator SilentMode( bool silent )
		{
			silentMode = silent;
			return this;
		}

		public void Cancel()
		{
			cancel = true;
		}

		private void Log( string log )
		{
			if( !silentMode && !cancel )
			{
				lock( logs )
				{
					logs.Enqueue( log );
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

		public bool Run()
		{
			if( !IsRunning )
			{
				IsRunning = true;
				cancel = false;

				PatchUtils.CreateBackgroundThread( new ThreadStart( ThreadCreatePatchFunction ) ).Start();
				return true;
			}

			return false;
		}

		private void ThreadCreatePatchFunction()
		{
			try
			{
				Result = CreatePatch();
			}
			catch( Exception e )
			{
				Log( e.ToString() );
				Result = PatchResult.Failed;
			}

			IsRunning = false;
		}

		/// <exception cref = "FormatException">projectName contains invalid character(s)</exception>
		private PatchResult CreatePatch()
		{
			versionInfo = new VersionInfo()
			{
				Name = projectName, // throws FormatException if 'projectName' contains invalid character(s)
				Version = version,
				BaseDownloadURL = baseDownloadURL,
				MaintenanceCheckURL = maintenanceCheckURL,
				CompressionFormat = compressionFormat
			};

			PatchUtils.DeleteDirectory( outputPath );
			Directory.CreateDirectory( outputPath );

			if( cancel )
				return PatchResult.Failed;

			Log( Localization.Get( StringId.GeneratingListOfFilesInBuild ) );

			AddFilesToVersionRecursively( new DirectoryInfo( rootPath ), "" );

			if( cancel )
				return PatchResult.Failed;

			if( generateRepairPatch && CreateRepairPatch() == PatchResult.Failed )
				return PatchResult.Failed;

			if( generateInstallerPatch && CreateInstallerPatch() == PatchResult.Failed )
				return PatchResult.Failed;

			if( GenerateIncrementalPatch && CreateIncrementalPatch() == PatchResult.Failed )
				return PatchResult.Failed;

			PatchUtils.SetVersion( rootPath, projectName, version );

			versionInfo.IgnoredPaths.AddRange( ignoredPaths );

			Log( Localization.Get( StringId.WritingVersionInfoToXML ) );
			PatchUtils.SerializeVersionInfoToXML( versionInfo, outputPath + PatchParameters.VERSION_INFO_FILENAME );

			Log( Localization.Get( StringId.Done ) );

			return PatchResult.Success;
		}

		private PatchResult CreateRepairPatch()
		{
			if( cancel )
				return PatchResult.Failed;

			Directory.CreateDirectory( repairPatchOutputPath );

			Log( Localization.Get( StringId.CreatingRepairPatch ) );
			Stopwatch timer = Stopwatch.StartNew();

			// Compress repair patch files and move them to the destination
			Log( Localization.Get( StringId.CompressingFilesToDestination ) );
			Stopwatch compressTimer = Stopwatch.StartNew();

			for( int i = 0; i < versionInfo.Files.Count; i++ )
			{
				if( cancel )
					return PatchResult.Failed;

				VersionItem patchItem = versionInfo.Files[i];
				string fromAbsolutePath = rootPath + patchItem.Path;
				string toAbsolutePath = repairPatchOutputPath + patchItem.Path + PatchParameters.REPAIR_PATCH_FILE_EXTENSION;

				Log( Localization.Get( StringId.CompressingXToY, fromAbsolutePath, toAbsolutePath ) );
				compressTimer.Reset();
				compressTimer.Start();

				ZipUtils.CompressFile( fromAbsolutePath, toAbsolutePath, compressionFormat );
				Log( Localization.Get( StringId.CompressionFinishedInXSeconds, compressTimer.ElapsedSeconds() ) );

				patchItem.OnCompressed( new FileInfo( toAbsolutePath ) );
			}

			if( cancel )
				return PatchResult.Failed;

			Log( Localization.Get( StringId.PatchCreatedInXSeconds, timer.ElapsedSeconds() ) );

			// Calculate compression ratio
			long uncompressedTotal = 0L, compressedTotal = 0L;
			for( int i = 0; i < versionInfo.Files.Count; i++ )
			{
				uncompressedTotal += versionInfo.Files[i].FileSize;
				compressedTotal += versionInfo.Files[i].CompressedFileSize;
			}

			Log( Localization.Get( StringId.CompressionRatioIsX, ( (double) compressedTotal * 100 / uncompressedTotal ).ToString( "F2" ) ) );

			return PatchResult.Success;
		}

		private PatchResult CreateInstallerPatch()
		{
			if( cancel )
				return PatchResult.Failed;

			Directory.CreateDirectory( installerPatchOutputPath );
			string compressedPatchPath = installerPatchOutputPath + PatchParameters.INSTALLER_PATCH_FILENAME;

			Log( Localization.Get( StringId.CreatingInstallerPatch ) );
			Stopwatch timer = Stopwatch.StartNew();

			Log( Localization.Get( StringId.CompressingXToY, rootPath, compressedPatchPath ) );
			ZipUtils.CompressFolder( rootPath, compressedPatchPath, compressionFormat, ignoredPathsRegex );

			if( cancel )
				return PatchResult.Failed;

			Log( Localization.Get( StringId.PatchCreatedInXSeconds, timer.ElapsedSeconds() ) );

			FileInfo installerPatch = new FileInfo( compressedPatchPath );
			versionInfo.InstallerPatch = new InstallerPatch( installerPatch );

			// Calculate compression ratio
			long uncompressedTotal = 0L, compressedTotal = installerPatch.Length;
			for( int i = 0; i < versionInfo.Files.Count; i++ )
				uncompressedTotal += versionInfo.Files[i].FileSize;

			Log( Localization.Get( StringId.CompressionRatioIsX, ( (double) compressedTotal * 100 / uncompressedTotal ).ToString( "F2" ) ) );

			return PatchResult.Success;
		}

		private PatchResult CreateIncrementalPatch()
		{
			if( cancel )
				return PatchResult.Failed;

			Directory.CreateDirectory( incrementalPatchOutputPath );
			Directory.CreateDirectory( incrementalPatchTempPath );

			incrementalPatch = new IncrementalPatchInfo
			{
				FromVersion = previousVersion,
				ToVersion = version
			};

			Log( Localization.Get( StringId.CreatingIncrementalPatch ) );
			Stopwatch timer = Stopwatch.StartNew();

			DirectoryInfo rootDirectory = new DirectoryInfo( rootPath );
			TraverseIncrementalPatchRecursively( rootDirectory, "" );

			if( cancel )
				return PatchResult.Failed;

			Log( Localization.Get( StringId.CompressingPatchIntoOneFile ) );
			string compressedPatchPath = incrementalPatchOutputPath + incrementalPatch.PatchVersion() + PatchParameters.INCREMENTAL_PATCH_FILE_EXTENSION;
			ZipUtils.CompressFolder( incrementalPatchTempPath, compressedPatchPath, compressionFormat );

			Log( Localization.Get( StringId.WritingIncrementalPatchInfoToXML ) );
			PatchUtils.SerializeIncrementalPatchInfoToXML( incrementalPatch, incrementalPatchOutputPath + incrementalPatch.PatchVersion() + PatchParameters.INCREMENTAL_PATCH_INFO_EXTENSION );

			versionInfo.IncrementalPatches.Add( new IncrementalPatch( previousVersion, version, new FileInfo( compressedPatchPath ), incrementalPatch.Files.Count ) );

			PatchUtils.DeleteDirectory( incrementalPatchTempPath );

			Log( Localization.Get( StringId.IncrementalPatchCreatedInXSeconds, timer.ElapsedSeconds() ) );

			return PatchResult.Success;
		}

		private void AddFilesToVersionRecursively( DirectoryInfo directory, string relativePath )
		{
			if( cancel )
				return;

			FileInfo[] files = directory.GetFiles();
			for( int i = 0; i < files.Length; i++ )
			{
				string fileRelativePath = relativePath + files[i].Name;
				if( !ignoredPathsRegex.PathMatchesPattern( fileRelativePath ) )
					versionInfo.Files.Add( new VersionItem( fileRelativePath, files[i] ) );
			}

			DirectoryInfo[] subDirectories = directory.GetDirectories();
			for( int i = 0; i < subDirectories.Length; i++ )
			{
				string directoryRelativePath = relativePath + subDirectories[i].Name + Path.DirectorySeparatorChar;
				if( !ignoredPathsRegex.PathMatchesPattern( directoryRelativePath ) )
				{
					if( generateRepairPatch )
						Directory.CreateDirectory( repairPatchOutputPath + directoryRelativePath );

					AddFilesToVersionRecursively( subDirectories[i], directoryRelativePath );
				}
			}
		}

		private void TraverseIncrementalPatchRecursively( DirectoryInfo directory, string relativePath )
		{
			FileInfo[] files = directory.GetFiles();
			for( int i = 0; i < files.Length; i++ )
			{
				if( cancel )
					return;

				FileInfo fileInfo = files[i];
				string fileRelativePath = relativePath + fileInfo.Name;
				if( !ignoredPathsRegex.PathMatchesPattern( fileRelativePath ) )
				{
					string targetAbsolutePath = previousVersionRoot + fileRelativePath;
					string diffFileAbsolutePath = incrementalPatchTempPath + fileRelativePath;

					if( !File.Exists( targetAbsolutePath ) )
					{
						Log( Localization.Get( StringId.CopyingXToPatch, fileRelativePath ) );

						PatchUtils.CopyFile( fileInfo.FullName, diffFileAbsolutePath );
						incrementalPatch.Files.Add( new PatchItem( fileRelativePath, null, fileInfo ) );
					}
					else
					{
						FileInfo prevVersion = new FileInfo( targetAbsolutePath );
						if( !fileInfo.MatchesSignature( prevVersion ) )
						{
							Log( Localization.Get( StringId.CalculatingDiffOfX, fileRelativePath ) );

							OctoUtils.CalculateDelta( targetAbsolutePath, fileInfo.FullName, diffFileAbsolutePath );
							incrementalPatch.Files.Add( new PatchItem( fileRelativePath, prevVersion, fileInfo ) );
						}
					}
				}
			}

			DirectoryInfo[] subDirectories = directory.GetDirectories();
			for( int i = 0; i < subDirectories.Length; i++ )
			{
				if( cancel )
					return;

				string directoryRelativePath = relativePath + subDirectories[i].Name + Path.DirectorySeparatorChar;
				if( !ignoredPathsRegex.PathMatchesPattern( directoryRelativePath ) )
				{
					Directory.CreateDirectory( incrementalPatchTempPath + directoryRelativePath );
					TraverseIncrementalPatchRecursively( subDirectories[i], directoryRelativePath );
				}
			}
		}
	}
}