using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SimplePatchToolCore
{
	internal class RepairPatchApplier : PatchMethodBase
	{
		public RepairPatchApplier( PatchIntercomms comms ) : base( comms )
		{
		}

		protected override PatchResult Execute()
		{
			if( comms.Cancel )
				return PatchResult.Failed;

			if( comms.IsUnderMaintenance() )
				return PatchResult.Failed;

			Stopwatch timer = Stopwatch.StartNew();

			comms.Stage = PatchStage.CalculatingFilesToUpdate;

			comms.Log( Localization.Get( StringId.CalculatingNewOrChangedFiles ) );
			List<VersionItem> filesToUpdate = comms.FindFilesToUpdate();

			if( filesToUpdate.Count == 0 )
				return PatchResult.AlreadyUpToDate;

			if( comms.Cancel )
				return PatchResult.Failed;

			comms.Log( Localization.Get( StringId.CalculatingFilesToDownload ) );
			List<VersionItem> filesToDownload = FindFilesToDownload( filesToUpdate );

			if( comms.Cancel )
				return PatchResult.Failed;

			long estimatedDownloadSize = 0L;
			for( int i = 0; i < filesToDownload.Count; i++ )
				estimatedDownloadSize += filesToDownload[i].CompressedFileSize;

			InitializeProgress( filesToUpdate.Count, estimatedDownloadSize );

			if( filesToDownload.Count > 0 && comms.VerifyFiles )
			{
				comms.Stage = PatchStage.VerifyingFilesOnServer;

				for( int i = 0; i < filesToDownload.Count; i++ )
				{
					if( comms.Cancel )
						return PatchResult.Failed;

					VersionItem item = filesToDownload[i];
					long fileSize;
					if( !comms.DownloadManager.FileExistsAtUrl( comms.VersionInfo.GetDownloadURLFor( item ), out fileSize ) )
					{
						comms.FailReason = PatchFailReason.FileDoesNotExistOnServer;
						comms.FailDetails = Localization.Get( StringId.E_FileXDoesNotExistOnServer, item.Path );

						return PatchResult.Failed;
					}
					else if( fileSize > 0L && fileSize != item.CompressedFileSize )
					{
						comms.FailReason = PatchFailReason.FileIsNotValidOnServer;
						comms.FailDetails = Localization.Get( StringId.E_FileXIsNotValidOnServer, item.Path );

						return PatchResult.Failed;
					}
				}
			}

			if( filesToDownload.Count > 0 )
				comms.Log( Localization.Get( StringId.DownloadingXFiles, filesToDownload.Count ) );

			if( filesToUpdate.Count > 0 )
				comms.Log( Localization.Get( StringId.UpdatingXFiles, filesToUpdate.Count ) );

			Stopwatch downloadTimer = Stopwatch.StartNew();

			if( !DownloadAndUpdateFiles( filesToDownload, filesToUpdate ) )
				return PatchResult.Failed;

			comms.Log( Localization.Get( StringId.AllFilesAreDownloadedInXSeconds, downloadTimer.ElapsedSeconds() ) );

			PatchUtils.DeleteDirectory( comms.DownloadsPath );

			comms.Log( Localization.Get( StringId.PatchAppliedInXSeconds, timer.ElapsedSeconds() ) );

			return PatchResult.Success;
		}

		private List<VersionItem> FindFilesToDownload( List<VersionItem> filesToUpdate )
		{
			List<VersionItem> result = new List<VersionItem>();
			for( int i = 0; i < filesToUpdate.Count; i++ )
			{
				if( comms.Cancel )
					return null;

				VersionItem item = filesToUpdate[i];
				FileInfo downloadedFile = new FileInfo( comms.DownloadsPath + item.Path );
				if( !downloadedFile.Exists || !downloadedFile.MatchesSignature( item.CompressedFileSize, item.CompressedMd5Hash ) )
					result.Add( item );
			}

			return result;
		}

		private bool DownloadAndUpdateFiles( List<VersionItem> filesToDownload, List<VersionItem> filesToUpdate )
		{
			string rootPath = comms.SelfPatching ? comms.DecompressedFilesPath : comms.RootPath;

			for( int i = 0, j = 0; i < filesToUpdate.Count; i++ )
			{
				if( comms.Cancel )
					return false;

				VersionItem item = filesToUpdate[i];
				string downloadAbsolutePath = comms.DownloadsPath + item.Path;

				if( filesToDownload.Count > 0 && filesToDownload[j] == item )
				{
					comms.Stage = PatchStage.DownloadingFiles;

					// Download the file
					Directory.CreateDirectory( Path.GetDirectoryName( downloadAbsolutePath ) );

					comms.Log( Localization.Get( StringId.DownloadingXthFile, j + 1, filesToDownload.Count, item.Path, item.CompressedFileSize.ToMegabytes() ) );
					Stopwatch downloadTimer = Stopwatch.StartNew();

					FileInfo downloadedFile = comms.DownloadManager.DownloadFileFromURLToPath( comms.VersionInfo.GetDownloadURLFor( item ), downloadAbsolutePath, item.CompressedFileSize );
					if( downloadedFile == null )
					{
						comms.FailReason = PatchFailReason.DownloadError;
						comms.FailDetails = Localization.Get( StringId.E_XCouldNotBeDownloaded, item.Path );

						return false;
					}
					else if( !downloadedFile.MatchesSignature( item.CompressedFileSize, item.CompressedMd5Hash ) )
					{
						comms.FailReason = PatchFailReason.CorruptDownloadError;
						comms.FailDetails = Localization.Get( StringId.E_DownloadedFileXIsCorrupt, item.Path );

						return false;
					}
					else
						comms.Log( Localization.Get( StringId.XDownloadedInYSeconds, item.Path, downloadTimer.ElapsedSeconds() ) );

					j++;
				}

				if( comms.Cancel )
					return false;

				comms.Stage = PatchStage.UpdatingFiles;
				comms.Log( Localization.Get( StringId.UpdatingXthFile, i + 1, filesToUpdate.Count, item.Path ) );

				string targetAbsolutePath = rootPath + item.Path;
				Directory.CreateDirectory( Path.GetDirectoryName( targetAbsolutePath ) );
				ZipUtils.DecompressFile( downloadAbsolutePath, targetAbsolutePath, comms.VersionInfo.CompressionFormat );

				File.Delete( downloadAbsolutePath );
				ReportProgress( 1, 0L );
			}

			return true;
		}
	}
}