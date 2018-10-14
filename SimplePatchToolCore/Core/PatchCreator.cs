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
		private VersionInfo patch;
		private PatchInfo incrementalPatch;

		private readonly string rootPath;
		private readonly string outputPath;
		private readonly string repairPatchOutputPath;
		private readonly string incrementalPatchOutputPath;
		private readonly string incrementalPatchTempPath;
		private readonly string projectName;

		private string previousVersionRoot;

		private VersionCode previousVersion;
		private readonly VersionCode version;

		private bool GenerateIncrementalPatch { get { return !string.IsNullOrEmpty( previousVersionRoot ); } }
		private bool generateRepairPatch;

		private HashSet<string> ignoredPaths;
		private List<Regex> ignoredPathsRegex;

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

			previousVersionRoot = null;
			generateRepairPatch = false;

			this.previousVersion = null;
			this.version = version;

			this.rootPath = PatchUtils.GetPathWithTrailingSeparatorChar( rootPath );
			this.outputPath = PatchUtils.GetPathWithTrailingSeparatorChar( outputPath );
			this.projectName = projectName;

			repairPatchOutputPath = this.outputPath + PatchParameters.REPAIR_PATCH_DIRECTORY + Path.DirectorySeparatorChar;
			incrementalPatchOutputPath = this.outputPath + PatchParameters.INCREMENTAL_PATCH_DIRECTORY + Path.DirectorySeparatorChar;
			incrementalPatchTempPath = this.outputPath + "Temp" + Path.DirectorySeparatorChar;

			ignoredPaths = new HashSet<string>();
			ignoredPathsRegex = new List<Regex>() { PatchUtils.WildcardToRegex( "*" + PatchParameters.VERSION_HOLDER_FILENAME_POSTFIX ) }; // Ignore any version holder files

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

				Thread workerThread = new Thread( new ThreadStart( ThreadCreatePatchFunction ) ) { IsBackground = true };
				workerThread.Start();

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
				Result = PatchResult.Failed;
				Log( e.ToString() );
			}

			IsRunning = false;
		}

		/// <exception cref = "FormatException">projectName contains invalid character(s)</exception>
		private PatchResult CreatePatch()
		{
			patch = new VersionInfo()
			{
				Name = projectName, // throws FormatException if 'projectName' contains invalid character(s)
				Version = version
			};

			PatchUtils.DeleteDirectory( outputPath );
			Directory.CreateDirectory( outputPath );

			if( cancel )
				return PatchResult.Failed;

			Log( Localization.Get( StringId.GeneratingListOfFilesInBuild ) );
			CreateFileList();

			if( cancel )
				return PatchResult.Failed;

			if( generateRepairPatch && CreateRepairPatch() == PatchResult.Failed )
				return PatchResult.Failed;

			if( GenerateIncrementalPatch && CreateIncrementalPatch() == PatchResult.Failed )
				return PatchResult.Failed;

			PatchUtils.SetVersion( rootPath, projectName, version );

			patch.IgnoredPaths.AddRange( ignoredPaths );

			Log( Localization.Get( StringId.WritingVersionInfoToXML ) );
			PatchUtils.SerializeVersionInfoToXML( patch, outputPath + PatchParameters.VERSION_INFO_FILENAME );

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

			Log( Localization.Get( StringId.CompressingFilesToDestination ) );
			CompressRepairItemsToDestination();

			if( cancel )
				return PatchResult.Failed;

			Log( Localization.Get( StringId.PatchCreatedInXSeconds, timer.ElapsedSeconds() ) );
			Log( Localization.Get( StringId.CompressionRatioIsX, ( CalculateRepairPatchCompressionRatio() * 100 ).ToString( "F2" ) ) );

			return PatchResult.Success;
		}

		private PatchResult CreateIncrementalPatch()
		{
			if( cancel )
				return PatchResult.Failed;

			Directory.CreateDirectory( incrementalPatchOutputPath );
			Directory.CreateDirectory( incrementalPatchTempPath );

			incrementalPatch = new PatchInfo
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
			string compressedPatchPath = incrementalPatchOutputPath + incrementalPatch.PatchVersion() + PatchParameters.PATCH_FILE_EXTENSION;
			ZipUtils.CompressFolderLZMA( incrementalPatchTempPath, compressedPatchPath );

			Log( Localization.Get( StringId.WritingIncrementalPatchInfoToXML ) );
			PatchUtils.SerializePatchInfoToXML( incrementalPatch, incrementalPatchOutputPath + incrementalPatch.PatchVersion() + PatchParameters.PATCH_INFO_EXTENSION );

			patch.Patches.Add( new IncrementalPatch( previousVersion, version, new FileInfo( compressedPatchPath ) ) );

			PatchUtils.DeleteDirectory( incrementalPatchTempPath );

			Log( Localization.Get( StringId.IncrementalPatchCreatedInXSeconds, timer.ElapsedSeconds() ) );

			return PatchResult.Success;
		}

		private void CreateFileList()
		{
			DirectoryInfo rootDirectory = new DirectoryInfo( rootPath );
			AddFilesToPatchRecursively( rootDirectory, "" );
		}

		private void AddFilesToPatchRecursively( DirectoryInfo directory, string relativePath )
		{
			if( cancel )
				return;

			FileInfo[] files = directory.GetFiles();
			for( int i = 0; i < files.Length; i++ )
			{
				string fileRelativePath = relativePath + files[i].Name;
				if( !ignoredPathsRegex.PathMatchesPattern( fileRelativePath ) )
					patch.Files.Add( new VersionItem( fileRelativePath, files[i] ) );
			}

			DirectoryInfo[] subDirectories = directory.GetDirectories();
			for( int i = 0; i < subDirectories.Length; i++ )
			{
				string directoryRelativePath = relativePath + subDirectories[i].Name + Path.DirectorySeparatorChar;
				if( !ignoredPathsRegex.PathMatchesPattern( directoryRelativePath ) )
				{
					if( generateRepairPatch )
						Directory.CreateDirectory( repairPatchOutputPath + directoryRelativePath );

					AddFilesToPatchRecursively( subDirectories[i], directoryRelativePath );
				}
			}
		}

		private void CompressRepairItemsToDestination()
		{
			Stopwatch timer = Stopwatch.StartNew();

			for( int i = 0; i < patch.Files.Count; i++ )
			{
				if( cancel )
					return;

				VersionItem patchItem = patch.Files[i];
				string fromAbsolutePath = rootPath + patchItem.Path;
				string toAbsolutePath = repairPatchOutputPath + patchItem.Path + PatchParameters.COMPRESSED_FILE_EXTENSION;

				Log( Localization.Get( StringId.CompressingXToY, fromAbsolutePath, toAbsolutePath ) );
				timer.Reset();
				timer.Start();

				ZipUtils.CompressFileLZMA( fromAbsolutePath, toAbsolutePath );
				Log( Localization.Get( StringId.CompressionFinishedInXSeconds, timer.ElapsedSeconds() ) );

				patchItem.OnCompressed( new FileInfo( toAbsolutePath ) );
			}
		}

		private double CalculateRepairPatchCompressionRatio()
		{
			long uncompressedTotal = 0L, compressedTotal = 0L;
			for( int i = 0; i < patch.Files.Count; i++ )
			{
				uncompressedTotal += patch.Files[i].FileSize;
				compressedTotal += patch.Files[i].CompressedFileSize;
			}

			return (double) compressedTotal / uncompressedTotal;
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

						fileInfo.CopyTo( diffFileAbsolutePath, true );
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