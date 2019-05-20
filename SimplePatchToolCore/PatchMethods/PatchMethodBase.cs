namespace SimplePatchToolCore
{
	internal abstract class PatchMethodBase : IOperationProgress, IDownloadListener
	{
		protected readonly PatchIntercomms comms;

		private const int FILE_OPERATIONS_PROGRESS_CONTRIBUTION = 50;
		private const int DOWNLOAD_SIZE_PROGRESS_CONTRIBUTION = 50;

		private float fileOperationsMultiplier;
		private double downloadSizeMultiplier;

		private int completedFileOperations;
		private long downloadedBytes;

		public bool IsUsed { get; set; }

		public int Percentage
		{
			get
			{
				int fileOpPercentage, downloadPercentage;
				if( fileOperationsMultiplier > 0f )
				{
					fileOpPercentage = (int) ( completedFileOperations * fileOperationsMultiplier + 0.5f );
					if( fileOpPercentage > FILE_OPERATIONS_PROGRESS_CONTRIBUTION )
						fileOpPercentage = FILE_OPERATIONS_PROGRESS_CONTRIBUTION;
				}
				else
					fileOpPercentage = FILE_OPERATIONS_PROGRESS_CONTRIBUTION;

				if( downloadSizeMultiplier > 0 )
				{
					downloadPercentage = (int) ( downloadedBytes * downloadSizeMultiplier + 0.5 );
					if( downloadPercentage > DOWNLOAD_SIZE_PROGRESS_CONTRIBUTION )
						downloadPercentage = DOWNLOAD_SIZE_PROGRESS_CONTRIBUTION;
				}
				else
					downloadPercentage = DOWNLOAD_SIZE_PROGRESS_CONTRIBUTION;

				return fileOpPercentage + downloadPercentage;
			}
		}

		public string ProgressInfo
		{
			get
			{
				if( this is RepairPatchApplier )
					return Localization.Get( StringId.ApplyingRepairPatch );
				else if( this is IncrementalPatchApplier )
					return Localization.Get( StringId.ApplyingIncrementalPatch );
				else
					return Localization.Get( StringId.ApplyingInstallerPatch );
			}
		}

		protected PatchMethodBase( PatchIntercomms comms )
		{
			this.comms = comms;
		}

		public PatchResult Run()
		{
			PatchResult result = Execute();

			if( fileOperationsMultiplier > 0f )
				completedFileOperations = (int) ( 150f / fileOperationsMultiplier );
			else
				completedFileOperations = 0;

			if( downloadSizeMultiplier > 0 )
				downloadedBytes = (long) ( 150 / downloadSizeMultiplier );
			else
				downloadedBytes = 0;

			IsUsed = false;
			return result;
		}

		protected abstract PatchResult Execute();

		protected void InitializeProgress( int numberOfFileOperations, long expectedDownloadSize )
		{
			if( !comms.LogProgress )
				return;

			completedFileOperations = 0;
			downloadedBytes = 0L;

			if( numberOfFileOperations > 0 )
				fileOperationsMultiplier = FILE_OPERATIONS_PROGRESS_CONTRIBUTION / numberOfFileOperations;
			else
				fileOperationsMultiplier = 0f;

			if( expectedDownloadSize > 0 )
				downloadSizeMultiplier = DOWNLOAD_SIZE_PROGRESS_CONTRIBUTION / expectedDownloadSize;
			else
				downloadSizeMultiplier = 0;

			comms.DownloadManager.SetDownloadListener( this );
		}

		protected void ReportProgress( int filesProcessed, long bytesDownloaded )
		{
			completedFileOperations += filesProcessed;
			downloadedBytes += bytesDownloaded;

			IsUsed = false;
		}

		public void DownloadedBytes( long bytes )
		{
			ReportProgress( 0, bytes );
		}
	}
}