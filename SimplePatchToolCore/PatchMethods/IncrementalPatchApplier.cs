using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

namespace SimplePatchToolCore
{
	internal class IncrementalPatchApplier : PatchMethodBase
	{
		private readonly IncrementalPatchInfo patchInfo;

		public IncrementalPatchApplier( PatchIntercomms comms, IncrementalPatchInfo patchInfo ) : base( comms )
		{
			this.patchInfo = patchInfo;
		}

		protected override PatchResult Execute()
		{
			if( comms.Cancel )
				return PatchResult.Failed;

			if( comms.IsUnderMaintenance() )
				return PatchResult.Failed;

			string patchVersion = patchInfo.PatchVersion();
			string patchDownloadPath = comms.GetDownloadPathForPatch( patchVersion );
			string patchDecompressPath = comms.GetDecompressPathForPatch( patchVersion );

			if( patchInfo.Files.Count > 0 )
			{
				PatchUtils.DeleteDirectory( patchDecompressPath );
				Directory.CreateDirectory( patchDecompressPath );
			}

			if( comms.Cancel )
				return PatchResult.Failed;

			InitializeProgress( patchInfo.Files.Count, patchInfo.CompressedFileSize );

			Stopwatch timer = Stopwatch.StartNew();

			if( patchInfo.Files.Count > 0 )
			{
				FileInfo patchFile = new FileInfo( patchDownloadPath );
				if( !patchFile.Exists || !patchFile.MatchesSignature( patchInfo.CompressedFileSize, patchInfo.CompressedMd5Hash ) )
				{
					comms.Stage = PatchStage.DownloadingFiles;

					Stopwatch downloadTimer = Stopwatch.StartNew();
					comms.Log( Localization.Get( StringId.DownloadingPatchX, patchInfo.PatchVersion() ) );

					patchFile = comms.DownloadManager.DownloadFileFromURLToPath( patchInfo.DownloadURL, patchDownloadPath, patchInfo.CompressedFileSize );
					if( patchFile == null )
					{
						comms.FailReason = PatchFailReason.DownloadError;
						comms.FailDetails = Localization.Get( StringId.E_PatchXCouldNotBeDownloaded, patchInfo.PatchVersion() );

						return PatchResult.Failed;
					}
					else if( !patchFile.MatchesSignature( patchInfo.CompressedFileSize, patchInfo.CompressedMd5Hash ) )
					{
						comms.FailReason = PatchFailReason.CorruptDownloadError;
						comms.FailDetails = Localization.Get( StringId.E_DownloadedFileXIsCorrupt, patchInfo.PatchVersion() );

						return PatchResult.Failed;
					}
					else
						comms.Log( Localization.Get( StringId.XDownloadedInYSeconds, patchInfo.PatchVersion(), downloadTimer.ElapsedSeconds() ) );
				}
				else
					ReportProgress( 0, patchInfo.CompressedFileSize );

				if( comms.Cancel )
					return PatchResult.Failed;

				comms.Stage = PatchStage.ExtractingFilesFromArchive;
				comms.Log( Localization.Get( StringId.DecompressingPatchX, patchInfo.PatchVersion() ) );

				ZipUtils.DecompressFolderLZMA( patchFile.FullName, patchDecompressPath );

				comms.Stage = PatchStage.UpdatingFiles;
				comms.Log( Localization.Get( StringId.UpdatingXFiles, patchInfo.Files.Count ) );

				int failedItemCount = 0;
				for( int i = 0; i < patchInfo.Files.Count; i++ )
				{
					if( comms.Cancel )
						return PatchResult.Failed;

					ReportProgress( 1, 0L );

					string fileRelativePath = patchInfo.Files[i].Path;
					string diffFileAbsolutePath = patchDecompressPath + fileRelativePath;
					if( !File.Exists( diffFileAbsolutePath ) )
					{
						comms.Log( Localization.Get( StringId.E_DiffOfXDoesNotExist, Path.GetFileName( fileRelativePath ) ) );

						failedItemCount++;
						continue;
					}

					string decompressAbsolutePath = comms.DecompressedFilesPath + fileRelativePath;
					if( comms.SelfPatching && ApplyDiffToFile( decompressAbsolutePath, diffFileAbsolutePath, decompressAbsolutePath, i ) )
						continue;

					string localFileAbsolutePath = comms.RootPath + fileRelativePath;
					string targetPath = comms.SelfPatching ? decompressAbsolutePath : localFileAbsolutePath;
					if( !ApplyDiffToFile( localFileAbsolutePath, diffFileAbsolutePath, targetPath, i ) )
						failedItemCount++;
				}

				comms.Log( Localization.Get( StringId.XFilesUpdatedSuccessfully, patchInfo.Files.Count - failedItemCount, patchInfo.Files.Count ) );
			}

			if( patchInfo.RenamedFiles.Count > 0 )
			{
				comms.Log( Localization.Get( StringId.RenamingXFiles, patchInfo.RenamedFiles.Count ) );
				if( !RenameItems( patchInfo.RenamedFiles ) )
					return PatchResult.Failed;
			}

			// Updating version code to the latest one will be done by SimplePatchTool, after checking
			// whether or not all files are correctly updated
			if( patchInfo.ToVersion < comms.VersionInfo.Version )
				comms.UpdateVersion( patchInfo.ToVersion );

			if( patchInfo.Files.Count > 0 )
			{
				PatchUtils.DeleteDirectory( patchDecompressPath );
				File.Delete( patchDownloadPath );
			}

			comms.Log( Localization.Get( StringId.PatchAppliedInXSeconds, timer.ElapsedSeconds() ) );
			return PatchResult.Success;
		}

		private bool ApplyDiffToFile( string filePath, string diffPath, string targetPath, int patchItemIndex )
		{
			PatchItem item = patchInfo.Files[patchItemIndex];

			FileInfo targetFile = new FileInfo( targetPath );
			if( targetFile.Exists && targetFile.MatchesSignature( item.AfterFileSize, item.AfterMd5Hash ) )
			{
				comms.Log( Localization.Get( StringId.AlreadyUpToDateXthFile, patchItemIndex + 1, patchInfo.Files.Count, item.Path ) );
				return true;
			}

			if( item.BeforeFileSize == 0L )
			{
				comms.Log( Localization.Get( StringId.CreatingXthFile, patchItemIndex + 1, patchInfo.Files.Count, item.Path ) );

				Directory.CreateDirectory( Path.GetDirectoryName( targetPath ) );
				PatchUtils.CopyFile( diffPath, targetPath );
				File.Delete( diffPath );

				return true;
			}

			FileInfo localFile = new FileInfo( filePath );
			if( !localFile.Exists || !localFile.MatchesSignature( item.BeforeFileSize, item.BeforeMd5Hash ) )
				return false;

			comms.Log( Localization.Get( StringId.UpdatingXthFile, patchItemIndex + 1, patchInfo.Files.Count, item.Path ) );

			FilePatchProgress progress = null;
			string tempOutputPath = diffPath + "_.tmp";
			if( comms.LogProgress )
			{
				progress = new FilePatchProgress( Path.GetFileName( filePath ) );
				comms.SetProgress( progress );
			}

			OctoUtils.ApplyDelta( filePath, tempOutputPath, diffPath, progress );

			FileInfo updatedFile = new FileInfo( tempOutputPath );
			if( !updatedFile.Exists || !updatedFile.MatchesSignature( item.AfterFileSize, item.AfterMd5Hash ) )
				return false;

			Directory.CreateDirectory( Path.GetDirectoryName( targetPath ) );
			PatchUtils.CopyFile( tempOutputPath, targetPath );
			File.Delete( tempOutputPath );
			File.Delete( diffPath );

			return true;
		}

		private bool RenameItems( List<PatchRenamedItem> items )
		{
			string rootPath = comms.SelfPatching ? comms.DecompressedFilesPath : comms.RootPath;

			for( int i = 0; i < items.Count; i++ )
			{
				if( comms.Cancel )
					return false;

				string fromAbsolutePath = rootPath + items[i].BeforePath;
				string toAbsolutePath = rootPath + items[i].AfterPath;
				if( File.Exists( fromAbsolutePath ) )
					PatchUtils.MoveFile( fromAbsolutePath, toAbsolutePath );
				else if( Directory.Exists( fromAbsolutePath ) )
					PatchUtils.MoveDirectory( fromAbsolutePath, toAbsolutePath );
			}

			return true;
		}
	}
}