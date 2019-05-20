using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SimplePatchToolCore
{
	internal class InstallerPatchApplier : PatchMethodBase
	{
		public InstallerPatchApplier( PatchIntercomms comms ) : base( comms )
		{
		}

		protected override PatchResult Execute()
		{
			if( comms.Cancel )
				return PatchResult.Failed;

			if( comms.IsUnderMaintenance() )
				return PatchResult.Failed;

			string rootPath = comms.SelfPatching ? comms.DecompressedFilesPath : comms.RootPath;
			string patchDecompressPath = comms.GetDecompressPathForPatch( PatchParameters.INSTALLER_PATCH_DIRECTORY );
			string patchDownloadPath = comms.DownloadsPath + PatchParameters.INSTALLER_PATCH_FILENAME;

			InstallerPatch patchInfo = comms.VersionInfo.InstallerPatch;

			Stopwatch timer = Stopwatch.StartNew();

			comms.Stage = PatchStage.CalculatingFilesToUpdate;

			comms.Log( Localization.Get( StringId.CalculatingNewOrChangedFiles ) );
			List<VersionItem> filesToUpdate = comms.FindFilesToUpdate();

			if( filesToUpdate.Count == 0 )
				return PatchResult.AlreadyUpToDate;

			if( comms.Cancel )
				return PatchResult.Failed;

			InitializeProgress( filesToUpdate.Count, patchInfo.PatchSize );

			PatchUtils.DeleteDirectory( patchDecompressPath );
			Directory.CreateDirectory( patchDecompressPath );

			FileInfo patchFile = new FileInfo( patchDownloadPath );
			if( !patchFile.Exists || !patchFile.MatchesSignature( patchInfo.PatchSize, patchInfo.PatchMd5Hash ) )
			{
				comms.Stage = PatchStage.DownloadingFiles;

				Stopwatch downloadTimer = Stopwatch.StartNew();
				comms.Log( Localization.Get( StringId.DownloadingPatchX, PatchParameters.INSTALLER_PATCH_FILENAME ) );

				patchFile = comms.DownloadManager.DownloadFileFromURLToPath( comms.VersionInfo.GetDownloadURLFor( patchInfo ), patchDownloadPath, patchInfo.PatchSize );
				if( patchFile == null )
				{
					comms.FailReason = PatchFailReason.DownloadError;
					comms.FailDetails = Localization.Get( StringId.E_XCouldNotBeDownloaded, PatchParameters.INSTALLER_PATCH_FILENAME );

					return PatchResult.Failed;
				}
				else if( !patchFile.MatchesSignature( patchInfo.PatchSize, patchInfo.PatchMd5Hash ) )
				{
					comms.FailReason = PatchFailReason.CorruptDownloadError;
					comms.FailDetails = Localization.Get( StringId.E_DownloadedFileXIsCorrupt, PatchParameters.INSTALLER_PATCH_FILENAME );

					return PatchResult.Failed;
				}
				else
					comms.Log( Localization.Get( StringId.XDownloadedInYSeconds, PatchParameters.INSTALLER_PATCH_FILENAME, downloadTimer.ElapsedSeconds() ) );
			}
			else
				ReportProgress( 0, patchInfo.PatchSize );

			if( comms.Cancel )
				return PatchResult.Failed;

			comms.Stage = PatchStage.ExtractingFilesFromArchive;
			comms.Log( Localization.Get( StringId.DecompressingPatchX, PatchParameters.INSTALLER_PATCH_FILENAME ) );

			ZipUtils.DecompressFolderLZMA( patchFile.FullName, patchDecompressPath );

			comms.Stage = PatchStage.UpdatingFiles;
			comms.Log( Localization.Get( StringId.UpdatingXFiles, filesToUpdate.Count ) );

			for( int i = 0; i < filesToUpdate.Count; i++ )
			{
				if( comms.Cancel )
					return PatchResult.Failed;

				string fileRelativePath = filesToUpdate[i].Path;
				string targetAbsolutePath = rootPath + fileRelativePath;

				comms.Log( Localization.Get( StringId.UpdatingXthFile, i + 1, filesToUpdate.Count, fileRelativePath ) );

				Directory.CreateDirectory( Path.GetDirectoryName( targetAbsolutePath ) );
				PatchUtils.CopyFile( patchDecompressPath + fileRelativePath, targetAbsolutePath );

				ReportProgress( 1, 0L );
			}

			PatchUtils.DeleteDirectory( patchDecompressPath );
			File.Delete( patchDownloadPath );

			comms.Log( Localization.Get( StringId.PatchAppliedInXSeconds, timer.ElapsedSeconds() ) );
			return PatchResult.Success;
		}
	}
}