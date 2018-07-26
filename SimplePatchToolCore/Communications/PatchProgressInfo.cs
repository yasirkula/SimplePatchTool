namespace SimplePatchToolCore
{
	public interface IOperationProgress
	{
		bool IsUsed { get; set; }

		int Percentage { get; }
		string ProgressInfo { get; }
	}

	public class FilePatchProgress : IOperationProgress, Octodiff.Diagnostics.IProgressReporter
	{
		public bool IsUsed { get; set; }

		public int Percentage { get; private set; }
		public string ProgressInfo { get; private set; }

		internal FilePatchProgress( string filename )
		{
			Percentage = 0;
			ProgressInfo = Localization.Get( StringId.UpdatingX, filename );
		}

		public void ReportProgress( string operation, long currentPosition, long total )
		{
			Percentage = (int) ( (double) currentPosition / total * 100.0 + 0.5 );
			IsUsed = false;
		}
	}

	public class DownloadProgress : IOperationProgress
	{
		internal readonly IDownloadHandler downloadHandler;

		public bool IsUsed { get; set; }

		public string Filename { get { return downloadHandler.DownloadedFilename; } }
		public long Speed { get; private set; }

		public long FileDownloadedSize { get; private set; }
		public long FileTotalSize { get { return downloadHandler.DownloadedFileSize; } }

		public string SpeedPretty
		{
			get
			{
				if( Speed < 1048576L ) // 1MB
					return Speed.ToKilobytes() + "KB/s";
				else
					return Speed.ToMegabytes() + "MB/s";
			}
		}

		public int Percentage { get { return FileTotalSize > 0L ? (int) ( ( (double) FileDownloadedSize / FileTotalSize ) * 100 ) : 0; } }
		public string ProgressInfo { get { return Localization.Get( StringId.DownloadingXProgressInfo, Filename, FileDownloadedSize.ToMegabytes(), FileTotalSize.ToMegabytes(), SpeedPretty ); } }

		internal DownloadProgress( IDownloadHandler downloadHandler )
		{
			this.downloadHandler = downloadHandler;
		}

		internal void UpdateValues( long speed, long fileDownloadedSize )
		{
			Speed = speed;
			FileDownloadedSize = fileDownloadedSize;
			IsUsed = false;
		}
	}
}