using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SimplePatchToolCore
{
	public class ProjectManager : PatchCreator.IListener
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
		private readonly string versionsPath;
		private readonly string outputPath;
		private readonly string selfPatcherPath;
		public readonly string utilitiesPath;

		private readonly string projectInfoPath;
		private readonly string downloadLinksPath;

		private PatchCreator patchCreator;

		private Queue<string> logs;

		private bool cancel;
		private bool silentMode;

		public bool IsRunning { get; private set; }
		public PatchResult Result { get; private set; }

		bool PatchCreator.IListener.ReceiveLogs { get { return true; } }

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
			versionsPath = this.projectRoot + PatchParameters.PROJECT_VERSIONS_DIRECTORY + Path.DirectorySeparatorChar;
			outputPath = this.projectRoot + PatchParameters.PROJECT_OUTPUT_DIRECTORY + Path.DirectorySeparatorChar;
			selfPatcherPath = this.projectRoot + PatchParameters.PROJECT_SELF_PATCHER_DIRECTORY + Path.DirectorySeparatorChar;
			utilitiesPath = this.projectRoot + PatchParameters.PROJECT_OTHER_DIRECTORY + Path.DirectorySeparatorChar;

			projectInfoPath = this.projectRoot + PatchParameters.PROJECT_SETTINGS_FILENAME;
			downloadLinksPath = utilitiesPath + PatchParameters.PROJECT_UPDATE_LINKS_FILENAME;

			Localization.Get( StringId.Done ); // Force the localization system to be initialized with the current culture/language

			logs = new Queue<string>();

			cancel = false;
			silentMode = false;

			IsRunning = false;
			Result = PatchResult.Failed;
		}

		/// <exception cref = "IOException">Project root is not empty</exception>
		public void CreateProject()
		{
			if( Directory.Exists( projectRoot ) )
			{
				if( Directory.GetFiles( projectRoot ).Length > 0 || Directory.GetDirectories( projectRoot ).Length > 0 )
					throw new IOException( Localization.Get( StringId.E_DirectoryXIsNotEmpty, projectRoot ) );
			}

			Directory.CreateDirectory( versionsPath );
			Directory.CreateDirectory( outputPath );
			Directory.CreateDirectory( utilitiesPath );
			Directory.CreateDirectory( selfPatcherPath );

			PatchUtils.SerializeProjectInfoToXML( new ProjectInfo(), projectInfoPath );
			File.WriteAllText( downloadLinksPath, "PASTE DOWNLOAD LINKS OF THE PATCH FILES HERE" );
		}

		/// <exception cref = "FileNotFoundException">A necessary file does not exist</exception>
		public void UpdateDownloadLinks()
		{
			if( !File.Exists( downloadLinksPath ) )
				throw new FileNotFoundException( Localization.Get( StringId.E_XDoesNotExist, downloadLinksPath ) );

			PatchUpdater patchUpdater = new PatchUpdater( outputPath + PatchParameters.VERSION_INFO_FILENAME, Log );
			if( patchUpdater.UpdateDownloadLinks( downloadLinksPath ) )
				patchUpdater.SaveChanges();
		}

		public string[] GetXMLFiles( bool includeVersionInfo = true, bool includePatchInfos = true )
		{
			List<string> result = new List<string>();
			if( includeVersionInfo )
			{
				string versionInfoPath = outputPath + PatchParameters.VERSION_INFO_FILENAME;
				if( File.Exists( versionInfoPath ) )
					result.Add( versionInfoPath );
			}
			if( includePatchInfos )
			{
				string incrementalPatchesPath = outputPath + PatchParameters.INCREMENTAL_PATCH_DIRECTORY;
				if( Directory.Exists( incrementalPatchesPath ) )
				{
					string[] patchInfos = Directory.GetFiles( incrementalPatchesPath, "*" + PatchParameters.INCREMENTAL_PATCH_INFO_EXTENSION );
					for( int i = 0; i < patchInfos.Length; i++ )
						result.Add( patchInfos[i] );
				}
			}

			return result.ToArray();
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

		void PatchCreator.IListener.LogReceived( string log )
		{
			Log( log );
		}

		void PatchCreator.IListener.Finished()
		{
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
			//   Move the incremental patch files from there to Temp\IncrementalFormer
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
				PatchUtils.CopyDirectory( selfPatcherPath, Path.Combine( latestVersion, PatchParameters.SELF_PATCHER_DIRECTORY ) );

			patchCreator = new PatchCreator( latestVersion, tempOutput, projectInfo.Name, Path.GetFileName( latestVersion ) ).SetListener( this ).
				SetCompressionFormat( projectInfo.CompressionFormatRepairPatch, projectInfo.CompressionFormatInstallerPatch, projectInfo.CompressionFormatIncrementalPatch ).
				CreateRepairPatch( projectInfo.CreateRepairPatch ).CreateInstallerPatch( projectInfo.CreateInstallerPatch ).CreateIncrementalPatch( false ).
				AddIgnoredPaths( projectInfo.IgnoredPaths ).SilentMode( silentMode ).SetBaseDownloadURL( projectInfo.BaseDownloadURL ).SetMaintenanceCheckURL( projectInfo.MaintenanceCheckURL );

			// Generate repair patch and installer patch files
			if( cancel || !ExecuteCurrentPatch() )
				return PatchResult.Failed;

			if( projectInfo.CreateIncrementalPatch && versions.Length > 1 )
			{
				string incrementalPatchesGenerated = tempIncrementalOutput + PatchParameters.INCREMENTAL_PATCH_DIRECTORY;
				string versionInfoGenerated = tempIncrementalOutput + PatchParameters.VERSION_INFO_FILENAME;

				patchCreator = new PatchCreator( latestVersion, tempIncrementalOutput, projectInfo.Name, Path.GetFileName( latestVersion ) ).
					SetCompressionFormat( projectInfo.CompressionFormatRepairPatch, projectInfo.CompressionFormatInstallerPatch, projectInfo.CompressionFormatIncrementalPatch ).
					SetListener( this ).AddIgnoredPaths( projectInfo.IgnoredPaths ).SilentMode( silentMode ).CreateRepairPatch( false ).CreateInstallerPatch( false );

				for( int i = versions.Length - 2; i >= 0; i-- )
				{
					Log( Localization.Get( StringId.CreatingIncrementalPatchX, Path.GetFileName( versions[i] ) + "->" + Path.GetFileName( latestVersion ) ) );

					// Generate incremental patch files
					patchCreator.CreateIncrementalPatch( true, versions[i], projectInfo.BinaryDiffQuality );
					if( cancel || !ExecuteCurrentPatch() )
						return PatchResult.Failed;

					List<IncrementalPatch> newIncrementalPatches = PatchUtils.GetVersionInfoFromPath( versionInfoGenerated ).IncrementalPatches;
					for( int j = incrementalPatches.Count - 1; j >= 0; j-- )
					{
						// Don't allow duplicate IncrementalPatch entries
						for( int k = newIncrementalPatches.Count - 1; k >= 0; k-- )
						{
							if( incrementalPatches[j].FromVersion == newIncrementalPatches[k].FromVersion && incrementalPatches[j].ToVersion == newIncrementalPatches[k].ToVersion )
							{
								incrementalPatches.RemoveAt( j );
								break;
							}
						}
					}

					incrementalPatches.AddRange( newIncrementalPatches );

					// Move incremental patch files to Temp
					PatchUtils.MoveDirectory( incrementalPatchesGenerated, tempPrevIncrementalPatches );
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