using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace SimplePatchToolCore
{
	public delegate void DownloadStringCompletedEventHandler( bool cancelled, Exception error, string result, object userState );
	public delegate void DownloadFileCompletedEventHandler( bool cancelled, Exception error, object userState );
	public delegate void DownloadProgressChangedEventHandler( long bytesReceived, long totalBytesToReceive );

	internal class PatchDownloadManager
	{
		private readonly PatchIntercomms comms;

		private IDownloadHandler downloadHandler;
		private IDownloadListener downloadListener;

		private long lastDownloadBytes;
		private DateTime lastDownloadSpeedCalcTime;
		//private bool verifyDownloadSize;

		private string downloadStringResult;

		public PatchDownloadManager( PatchIntercomms comms )
		{
			this.comms = comms;
		}

		~PatchDownloadManager()
		{
			if( downloadHandler != null && downloadHandler is IDisposable )
				( (IDisposable) downloadHandler ).Dispose();
		}

		public void SetDownloadHandler( IDownloadHandler downloadHandler )
		{
			if( this.downloadHandler != null )
			{
				this.downloadHandler.OnDownloadStringComplete -= DownloadStringCompletedCallback;
				this.downloadHandler.OnDownloadFileComplete -= DownloadFileCompletedCallback;
				this.downloadHandler.OnDownloadFileProgressChange -= DownloadFileProgressChangedCallback;
			}

			this.downloadHandler = downloadHandler;

			downloadHandler.OnDownloadStringComplete += DownloadStringCompletedCallback;
			downloadHandler.OnDownloadFileComplete += DownloadFileCompletedCallback;
			downloadHandler.OnDownloadFileProgressChange += DownloadFileProgressChangedCallback;
		}

		public void SetDownloadListener( IDownloadListener downloadListener )
		{
			this.downloadListener = downloadListener;
		}

		#region Callback Functions
		// Credit: https://alexfeinberg.wordpress.com/2014/09/14/how-to-use-net-webclient-synchronously-and-still-receive-progress-updates/
		private void DownloadStringCompletedCallback( bool cancelled, Exception error, string result, object userState )
		{
			downloadStringResult = result;

			lock( userState )
			{
				Monitor.Pulse( userState );
			}
		}

		// Credit: https://alexfeinberg.wordpress.com/2014/09/14/how-to-use-net-webclient-synchronously-and-still-receive-progress-updates/
		private void DownloadFileCompletedCallback( bool cancelled, Exception error, object userState )
		{
			if( !cancelled && comms.LogProgress )
			{
				DateTime now = DateTime.Now;
				double deltaSeconds = ( now - lastDownloadSpeedCalcTime ).TotalSeconds;

				CalculateDownloadStats( downloadHandler.DownloadedFileSize, 0L, deltaSeconds );
			}

			lock( userState )
			{
				Monitor.Pulse( userState );
			}
		}

		// Credit: https://alexfeinberg.wordpress.com/2014/09/14/how-to-use-net-webclient-synchronously-and-still-receive-progress-updates/
		private void DownloadFileProgressChangedCallback( long bytesReceived, long totalBytesToReceive )
		{
			if( comms.Cancel )
			{
				downloadHandler.Cancel();
				return;
			}

			if( !comms.LogProgress )
				return;

			DateTime now = DateTime.Now;
			double deltaSeconds = ( now - lastDownloadSpeedCalcTime ).TotalSeconds;
			if( deltaSeconds >= PatchParameters.DownloadStatsUpdateInterval )
			{
				// See: https://github.com/yasirkula/UnitySimplePatchTool/issues/4
				//if( verifyDownloadSize )
				//{
				//	if( totalBytesToReceive > 0 && totalBytesToReceive != downloadHandler.DownloadedFileSize )
				//	{
				//		downloadHandler.Cancel();
				//		return;
				//	}
				//}

				if( CalculateDownloadStats( bytesReceived, totalBytesToReceive, deltaSeconds ) )
				{
					lastDownloadBytes = bytesReceived;
					lastDownloadSpeedCalcTime = now;
				}
			}
		}
		#endregion

		public MaintenanceCheckResult CheckForMaintenance( string maintenanceCheckURL )
		{
			if( string.IsNullOrEmpty( maintenanceCheckURL ) )
				return MaintenanceCheckResult.NoMaintenance;

			string maintenanceResult = DownloadTextFromURL( maintenanceCheckURL );
			if( string.IsNullOrEmpty( maintenanceResult ) || maintenanceResult[0] != '1' )
				return MaintenanceCheckResult.NoMaintenance;

			if( maintenanceResult.Length == 1 || maintenanceResult[1] != '1' )
				return MaintenanceCheckResult.Maintenance_CanLaunchApp;

			return MaintenanceCheckResult.Maintenance_AbortApp;
		}

		private bool IsGoogleDriveURL( string url )
		{
			return url.StartsWith( "https://drive.google.com", StringComparison.OrdinalIgnoreCase ) || url.StartsWith( "drive.google.com", StringComparison.OrdinalIgnoreCase );
		}

		private bool CalculateDownloadStats( long bytesReceived, long totalBytesToReceive, double deltaSeconds )
		{
			if( downloadHandler.Progress == null )
				return false;

			// Credit: http://stackoverflow.com/questions/11522577/webclient-downloadfileasync-how-can-i-display-download-speed-to-the-user
			long bytes = bytesReceived;
			long bytesChange = bytes - lastDownloadBytes;

			if( bytesChange > 0 )
			{
				if( downloadListener != null )
					downloadListener.DownloadedBytes( bytesChange );

				downloadHandler.Progress.UpdateValues( (long) ( bytesChange / deltaSeconds ), bytes );
				return true;
			}

			return false;
		}

		// Credit: http://stackoverflow.com/questions/1460273/how-to-check-if-a-file-exists-on-an-webserver-by-its-url
		// Credit: http://stackoverflow.com/questions/3614034/system-net-webexception-http-status-code
		public bool FileExistsAtUrl( string url, out long fileSize )
		{
			if( string.IsNullOrEmpty( url ) )
			{
				fileSize = 0L;
				return false;
			}

			if( IsGoogleDriveURL( url ) )
				url = PatchUtils.GetGoogleDriveDownloadLinkFromUrl( url );

			WebRequest request = WebRequest.Create( url );
			request.Method = "HEAD";
			request.Timeout = PatchParameters.FileAvailabilityCheckTimeout;

			try
			{
				using( HttpWebResponse response = request.GetResponse() as HttpWebResponse )
				{
					fileSize = response.ContentLength;
					return response.StatusCode == HttpStatusCode.OK;
				}
			}
			catch( WebException e )
			{
				fileSize = 0L;

				if( e.Response != null && e.Response is HttpWebResponse )
				{
					using( HttpWebResponse exResponse = (HttpWebResponse) e.Response )
					{
						if( exResponse.StatusCode == HttpStatusCode.ServiceUnavailable )
						{
							// Drive returns 503 error while requesting HEAD for valid download links
							if( IsGoogleDriveURL( url ) )
								return true;
						}
					}
				}

				comms.LogToFile( e );
				return false;
			}
		}

		public string DownloadTextFromURL( string url )
		{
			if( string.IsNullOrEmpty( url ) )
				return null;

			if( IsGoogleDriveURL( url ) )
				url = PatchUtils.GetGoogleDriveDownloadLinkFromUrl( url );

			for( int i = 0; i < PatchParameters.FailedDownloadsRetryLimit; i++ )
			{
				if( comms.Cancel )
					return null;

				try
				{
					var syncObject = new object();
					lock( syncObject )
					{
						downloadHandler.DownloadString( url, syncObject );
						Monitor.Wait( syncObject );
					}

					if( comms.Cancel )
						return null;

					if( downloadStringResult == null )
					{
						Thread.Sleep( PatchParameters.FailedDownloadsCooldownMillis );
						continue;
					}

					return downloadStringResult;
				}
				catch( WebException e )
				{
					comms.LogToFile( e );
					Thread.Sleep( PatchParameters.FailedDownloadsCooldownMillis );
				}
				catch( UriFormatException e )
				{
					comms.LogToFile( e );
					return null;
				}
			}

			return null;
		}

		public FileInfo DownloadFileFromURLToPath( string url, string path, long fileSize )
		{
			if( comms.Cancel )
				return null;

			if( string.IsNullOrEmpty( url ) || string.IsNullOrEmpty( path ) )
				return null;

			downloadHandler.DownloadedFilePath = path;
			downloadHandler.DownloadedFilename = Path.GetFileName( path );
			downloadHandler.DownloadedFileSize = fileSize;

			if( comms.LogProgress )
			{
				downloadHandler.Progress = new DownloadProgress( downloadHandler );
				comms.SetProgress( downloadHandler.Progress );
			}

			if( IsGoogleDriveURL( url ) )
			{
				url = PatchUtils.GetGoogleDriveDownloadLinkFromUrl( url );

				//verifyDownloadSize = false;
				return DownloadGoogleDriveFileFromURLToPath( url, path );
			}
			else
			{
				//verifyDownloadSize = true;
				return DownloadFileFromURLToPathInternal( url, path );
			}
		}

		private FileInfo DownloadFileFromURLToPathInternal( string url, string path )
		{
			long downloadedSize = 0L;
			for( int i = 0; i < PatchParameters.FailedDownloadsRetryLimit && downloadedSize < PatchParameters.FailedDownloadsRetrySizeLimit; i++, downloadedSize += lastDownloadBytes )
			{
				if( comms.Cancel )
					return null;

				lastDownloadBytes = 0L;
				lastDownloadSpeedCalcTime = DateTime.Now;

				try
				{
					var syncObject = new object();
					lock( syncObject )
					{
						downloadHandler.DownloadFile( url, path, syncObject );
						Monitor.Wait( syncObject );
					}

					if( comms.Cancel )
						return null;

					if( !File.Exists( path ) )
					{
						Thread.Sleep( PatchParameters.FailedDownloadsCooldownMillis );
						continue;
					}

					return new FileInfo( path );
				}
				catch( WebException e )
				{
					comms.LogToFile( e );
					Thread.Sleep( PatchParameters.FailedDownloadsCooldownMillis );
				}
				catch( UriFormatException e )
				{
					comms.LogToFile( e );
					return null;
				}
			}

			return null;
		}

		// Downloading large files from Google Drive prompts a warning screen and
		// requires manual confirmation. Consider that case and try to confirm the download automatically
		// if warning prompt occurs
		private FileInfo DownloadGoogleDriveFileFromURLToPath( string url, string path )
		{
			FileInfo downloadedFile;

			// Sometimes Drive returns an NID cookie instead of a download_warning cookie at first attempt,
			// but works in the second attempt
			for( int i = 0; i < 2; i++ )
			{
				downloadedFile = DownloadFileFromURLToPathInternal( url, path );
				if( downloadedFile == null )
					return null;

				// Confirmation page is around 50KB, shouldn't be larger than 65KB
				if( downloadedFile.Length > 65000 )
					return downloadedFile;

				// Downloaded file might be the confirmation page, check it
				string content;
				using( var reader = downloadedFile.OpenText() )
				{
					// Confirmation page starts with <!DOCTYPE html>, which can be preceeded by a newline
					char[] header = new char[20];
					int readCount = reader.ReadBlock( header, 0, 20 );
					if( readCount < 20 || !( new string( header ).Contains( "<!DOCTYPE html>" ) ) )
						return downloadedFile;

					content = reader.ReadToEnd();
				}

				int linkIndex = content.LastIndexOf( "href=\"/uc?" );
				if( linkIndex < 0 )
					return downloadedFile;

				linkIndex += 6;
				int linkEnd = content.IndexOf( '"', linkIndex );
				if( linkEnd < 0 )
					return downloadedFile;

				url = "https://drive.google.com" + content.Substring( linkIndex, linkEnd - linkIndex ).Replace( "&amp;", "&" );
			}

			downloadedFile = DownloadFileFromURLToPathInternal( url, path );

			return downloadedFile;
		}
	}

	#region Interfaces
	public interface IDownloadHandler
	{
		event DownloadStringCompletedEventHandler OnDownloadStringComplete;
		event DownloadFileCompletedEventHandler OnDownloadFileComplete;
		event DownloadProgressChangedEventHandler OnDownloadFileProgressChange;

		string DownloadedFilePath { get; set; }
		string DownloadedFilename { get; set; }
		long DownloadedFileSize { get; set; }
		DownloadProgress Progress { get; set; }

		void DownloadString( string url, object userState );
		void DownloadFile( string url, string path, object userState );

		void Cancel();
	}

	internal interface IDownloadListener
	{
		void DownloadedBytes( long bytes );
	}
	#endregion

	#region Helper Classes
	// Web client that supports Google Drive
	internal class CookieAwareWebClient : WebClient, IDownloadHandler
	{
		private class CookieContainer
		{
			Dictionary<string, string> _cookies;

			public string this[Uri url]
			{
				get
				{
					string cookie;
					if( _cookies.TryGetValue( url.Host, out cookie ) )
						return cookie;

					return null;
				}
				set
				{
					_cookies[url.Host] = value;
				}
			}

			public CookieContainer()
			{
				_cookies = new Dictionary<string, string>();
			}
		}

		public event DownloadStringCompletedEventHandler OnDownloadStringComplete;
		public event DownloadFileCompletedEventHandler OnDownloadFileComplete;
		public event DownloadProgressChangedEventHandler OnDownloadFileProgressChange;

		private readonly CookieContainer cookies;

		public string DownloadedFilePath { get; set; }
		public string DownloadedFilename { get; set; }
		public long DownloadedFileSize { get; set; }
		public DownloadProgress Progress { get; set; }

		public CookieAwareWebClient() : base()
		{
			cookies = new CookieContainer();
			Encoding = Encoding.UTF8;

			DownloadStringCompleted += DownloadStringCompletedCallback;
			DownloadFileCompleted += DownloadFileCompletedCallback;
			DownloadProgressChanged += DownloadFileProgressChangedCallback;
		}

		protected override WebRequest GetWebRequest( Uri address )
		{
			WebRequest request = base.GetWebRequest( address );

			if( request is HttpWebRequest )
			{
				string cookie = cookies[address];
				if( cookie != null )
					( (HttpWebRequest) request ).Headers.Set( "cookie", cookie );
			}

			return request;
		}

		protected override WebResponse GetWebResponse( WebRequest request, IAsyncResult result )
		{
			return ProcessResponse( base.GetWebResponse( request, result ) );
		}

		protected override WebResponse GetWebResponse( WebRequest request )
		{
			return ProcessResponse( base.GetWebResponse( request ) );
		}

		private WebResponse ProcessResponse( WebResponse response )
		{
			string[] cookies = response.Headers.GetValues( "Set-Cookie" );
			if( cookies != null && cookies.Length > 0 )
			{
				int length = 0;
				for( int i = 0; i < cookies.Length; i++ )
					length += cookies[i].Length;

				StringBuilder cookie = new StringBuilder( length );
				for( int i = 0; i < cookies.Length; i++ )
					cookie.Append( cookies[i] );

				this.cookies[response.ResponseUri] = cookie.ToString();
			}

			return response;
		}

		public void DownloadString( string url, object userState )
		{
			DownloadStringAsync( new Uri( url ), userState );
		}

		public void DownloadFile( string url, string path, object userState )
		{
			DownloadFileAsync( new Uri( url ), path, userState );
		}

		public void Cancel()
		{
			CancelAsync();
		}

		private void DownloadStringCompletedCallback( object sender, DownloadStringCompletedEventArgs e )
		{
			if( OnDownloadStringComplete != null )
				OnDownloadStringComplete( e.Cancelled, e.Error, ( !e.Cancelled && e.Error == null ) ? e.Result : null, e.UserState );
		}

		private void DownloadFileCompletedCallback( object sender, AsyncCompletedEventArgs e )
		{
			if( OnDownloadFileComplete != null )
				OnDownloadFileComplete( e.Cancelled, e.Error, e.UserState );
		}

		private void DownloadFileProgressChangedCallback( object sender, DownloadProgressChangedEventArgs e )
		{
			if( OnDownloadFileProgressChange != null )
				OnDownloadFileProgressChange( e.BytesReceived, e.TotalBytesToReceive );
		}
	}
	#endregion
}