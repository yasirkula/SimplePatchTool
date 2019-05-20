using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SimplePatchToolCore
{
	public class ProjectManager
	{
		private class VersionComparer : IComparer<string>
		{
			public int Compare( string x, string y )
			{
				VersionCode v1 = new VersionCode( Path.GetFileName( x ) );
				VersionCode v2 = new VersionCode( Path.GetFileName( y ) );

				if( !v1.IsValid )
					throw new FormatException( Localization.Get( StringId.E_VersionCodeXIsInvalid, x ) );
				if( !v2.IsValid )
					throw new FormatException( Localization.Get( StringId.E_VersionCodeXIsInvalid, y ) );

				return v1.CompareTo( v2 );
			}
		}

		private readonly string projectRoot;
		private readonly string projectInfoPath;
		private readonly string versionsPath;
		private readonly string outputPath;
		private readonly string selfPatcherPath;

		private PatchCreator patchCreator;

		private Queue<string> logs;

		private bool cancel;
		private bool silentMode;

		public bool IsRunning { get; private set; }
		public PatchResult Result { get; private set; }

		/// <exception cref = "UnauthorizedAccessException">A path needs admin priviledges to write</exception>
		/// <exception cref = "ArgumentException">An argument is empty</exception>
		public ProjectManager( string projectRoot )
		{
			projectRoot = projectRoot.Trim();

			if( string.IsNullOrEmpty( projectRoot ) )
				throw new ArgumentException( Localization.Get( StringId.E_XCanNotBeEmpty, "'projectRoot'" ) );

			if( !PatchUtils.CheckWriteAccessToFolder( projectRoot ) )
				throw new UnauthorizedAccessException( Localization.Get( StringId.E_AccessToXIsForbiddenRunInAdminMode, projectRoot ) );

			this.projectRoot = PatchUtils.GetPathWithTrailingSeparatorChar( projectRoot );
			projectInfoPath = this.projectRoot + PatchParameters.PROJECT_SETTINGS_FILENAME;
			versionsPath = this.projectRoot + PatchParameters.PROJECT_VERSIONS_DIRECTORY + Path.DirectorySeparatorChar;
			outputPath = this.projectRoot + PatchParameters.PROJECT_OUTPUT_DIRECTORY + Path.DirectorySeparatorChar;
			selfPatcherPath = this.projectRoot + PatchParameters.PROJECT_SELF_PATCHER_DIRECTORY + Path.DirectorySeparatorChar;

			Localization.Get( StringId.Done ); // Force the localization system to be initialized with the current culture/language

			logs = new Queue<string>();

			cancel = false;
			silentMode = false;

			IsRunning = false;
			Result = PatchResult.Failed;
		}

		public void CreateProject()
		{
			Directory.CreateDirectory( versionsPath );
			Directory.CreateDirectory( outputPath );
			Directory.CreateDirectory( selfPatcherPath );

			PatchUtils.SerializeProjectInfoToXML( new ProjectInfo(), projectInfoPath );
		}

		public ProjectManager SilentMode( bool silent )
		{
			silentMode = silent;

			if( patchCreator != null )
				patchCreator.SilentMode( silent );

			return this;
		}

		public void Cancel()
		{
			cancel = true;

			if( patchCreator != null )
				patchCreator.Cancel();
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
				if( logs.Count > 0 )
					return logs.Dequeue();
			}

			if( patchCreator != null )
				return patchCreator.FetchLog();

			return null;
		}

		public bool GeneratePatch()
		{
			if( !IsRunning )
			{
				IsRunning = true;
				cancel = false;

				PatchUtils.CreateBackgroundThread( new ThreadStart( ThreadGeneratePatchFunction ) ).Start();
				return true;
			}

			return false;
		}

		private void ThreadGeneratePatchFunction()
		{
			try
			{
				Result = GeneratePatchInternal();
			}
			catch( Exception e )
			{
				Log( e.ToString() );
				Result = PatchResult.Failed;
			}

			IsRunning = false;
		}

		private PatchResult GeneratePatchInternal()
		{
			if( !Directory.Exists( versionsPath ) )
			{
				Log( Localization.Get( StringId.E_XDoesNotExist, versionsPath ) );
				return PatchResult.Failed;
			}

			if( !File.Exists( projectInfoPath ) )
			{
				Log( Localization.Get( StringId.E_XDoesNotExist, projectInfoPath ) );
				return PatchResult.Failed;
			}

			string[] versions = Directory.GetDirectories( versionsPath );
			if( versions.Length == 0 )
			{
				Log( Localization.Get( StringId.E_DirectoryXIsEmpty, versionsPath ) );
				return PatchResult.Failed;
			}

			string validationResult = ValidateProject();
			if( !string.IsNullOrEmpty( validationResult ) )
			{
				Log( validationResult );
				return PatchResult.Failed;
			}

			// Here's how it works:
			// Move previous incremental patch files to Temp\IncrementalFormer
			// Generate repair patch and installer patch files on Temp\Output
			// Foreach incremental patch to generate:
			//   Generate incremental patch files on Temp\Incremental
			//   Move the incremental patch files from there to Temp\Output
			// Replace the contents of outputPath with Temp\Output:
			//   Delete outputPath directory
			//   Move Temp\Output to outputPath
			//   Move Temp\IncrementalFormer to outputPath
			// Delete Temp
			string tempRoot = projectRoot + "Temp" + Path.DirectorySeparatorChar;
			string tempOutput = tempRoot + "Output" + Path.DirectorySeparatorChar;
			string tempIncrementalOutput = tempRoot + "Incremental" + Path.DirectorySeparatorChar;
			string tempPrevIncrementalPatches = tempRoot + "IncrementalFormer" + Path.DirectorySeparatorChar;

			PatchUtils.DeleteDirectory( tempOutput );
			PatchUtils.DeleteDirectory( tempIncrementalOutput );

			// Preserve the previous incremental patches
			string versionInfoPath = outputPath + PatchParameters.VERSION_INFO_FILENAME;
			string incrementalPatchesPath = outputPath + PatchParameters.INCREMENTAL_PATCH_DIRECTORY;

			if( Directory.Exists( incrementalPatchesPath ) )
				PatchUtils.MoveDirectory( incrementalPatchesPath, tempPrevIncrementalPatches );

			List<IncrementalPatch> incrementalPatches = new List<IncrementalPatch>();
			if( File.Exists( versionInfoPath ) )
			{
				VersionInfo oldVersionInfo = PatchUtils.GetVersionInfoFromPath( versionInfoPath );
				if( oldVersionInfo != null )
					incrementalPatches.AddRange( oldVersionInfo.IncrementalPatches );
			}

			Array.Sort( versions, new VersionComparer() );

			string latestVersion = versions[versions.Length - 1];
			ProjectInfo projectInfo = PatchUtils.GetProjectInfoFromPath( projectInfoPath );

			if( projectInfo.IsSelfPatchingApp && Directory.Exists( selfPatcherPath ) )
				PatchUtils.CopyDirectory( selfPatcherPath, outputPath + PatchParameters.SELF_PATCHER_DIRECTORY );

			patchCreator = new PatchCreator( latestVersion, tempOutput, projectInfo.Name, Path.GetFileName( latestVersion ) ).
				AddIgnoredPaths( projectInfo.IgnoredPaths ).SilentMode( silentMode ).
				CreateRepairPatch( projectInfo.CreateRepairPatch ).CreateInstallerPatch( projectInfo.CreateInstallerPatch ).CreateIncrementalPatch( false ).
				SetBaseDownloadURL( projectInfo.BaseDownloadURL ).SetMaintenanceCheckURL( projectInfo.MaintenanceCheckURL );

			// Generate repair patch and installer patch files
			if( cancel || !ExecuteCurrentPatch() )
				return PatchResult.Failed;

			if( projectInfo.CreateIncrementalPatch && versions.Length > 1 )
			{
				string incrementalPatchesTarget = tempOutput + PatchParameters.INCREMENTAL_PATCH_DIRECTORY;
				string incrementalPatchesGenerated = tempIncrementalOutput + PatchParameters.INCREMENTAL_PATCH_DIRECTORY;
				string versionInfoGenerated = tempIncrementalOutput + PatchParameters.VERSION_INFO_FILENAME;

				patchCreator = new PatchCreator( latestVersion, tempIncrementalOutput, projectInfo.Name, Path.GetFileName( latestVersion ) ).
					AddIgnoredPaths( projectInfo.IgnoredPaths ).SilentMode( silentMode ).
					CreateRepairPatch( false ).CreateInstallerPatch( false );

				for( int i = versions.Length - 2; i >= 0; i-- )
				{
					Log( Localization.Get( StringId.CreatingIncrementalPatchX, Path.GetDirectoryName( versions[i] ) + "->" + Path.GetFileName( latestVersion ) ) );

					// Generate incremental patch files
					patchCreator.CreateIncrementalPatch( true, versions[i] );
					if( cancel || !ExecuteCurrentPatch() )
						return PatchResult.Failed;

					incrementalPatches.AddRange( PatchUtils.GetVersionInfoFromPath( versionInfoGenerated ).IncrementalPatches );

					// Move incremental patch files to Temp
					PatchUtils.MoveDirectory( incrementalPatchesGenerated, incrementalPatchesTarget );
					PatchUtils.DeleteDirectory( tempIncrementalOutput );

					if( !projectInfo.CreateAllIncrementalPatches )
						break;
				}
			}

			PatchUtils.DeleteDirectory( outputPath );
			PatchUtils.MoveDirectory( tempOutput, outputPath );

			if( Directory.Exists( tempPrevIncrementalPatches ) )
				PatchUtils.MoveDirectory( tempPrevIncrementalPatches, incrementalPatchesPath );

			VersionInfo versionInfo = PatchUtils.GetVersionInfoFromPath( versionInfoPath );
			incrementalPatches.Sort( PatchUtils.IncrementalPatchComparison );
			versionInfo.IncrementalPatches = incrementalPatches;
			PatchUtils.SerializeVersionInfoToXML( versionInfo, versionInfoPath );

			PatchUtils.DeleteDirectory( tempRoot );

			return PatchResult.Success;
		}

		private bool ExecuteCurrentPatch()
		{
			if( patchCreator.Run() )
			{
				while( patchCreator.IsRunning )
					Thread.Sleep( 1000 );

				return patchCreator.Result == PatchResult.Success;
			}

			return false;
		}

		// Returns null if project is valid
		private string ValidateProject()
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder( 300 );

			if( !Directory.Exists( versionsPath ) )
				sb.AppendLine( Localization.Get( StringId.E_DirectoryXMissing, versionsPath ) );
			if( !Directory.Exists( outputPath ) )
				sb.AppendLine( Localization.Get( StringId.E_DirectoryXMissing, outputPath ) );

			if( !File.Exists( projectInfoPath ) )
				sb.AppendLine( Localization.Get( StringId.E_FileXMissing, projectInfoPath ) );
			else
			{
				ProjectInfo projectInfo = null;
				try
				{
					projectInfo = PatchUtils.GetProjectInfoFromPath( projectInfoPath );
				}
				catch( Exception e )
				{
					sb.AppendLine( e.ToString() );
				}

				if( projectInfo == null )
					sb.AppendLine( Localization.Get( StringId.E_ProjectInfoCouldNotBeDeserializedFromX, projectInfoPath ) );
				else if( projectInfo.Version != ProjectInfo.LATEST_VERSION )
					sb.AppendLine( Localization.Get( StringId.E_ProjectInfoOutdated ) );
			}

			if( sb.Length == 0 )
				return null;

			return string.Concat( Localization.Get( StringId.E_ProjectInvalid ), Environment.NewLine, Environment.NewLine, sb.ToString() );
		}
	}
}