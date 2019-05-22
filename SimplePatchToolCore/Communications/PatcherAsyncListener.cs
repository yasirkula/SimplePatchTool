using System.Threading;

namespace SimplePatchToolCore
{
	public class PatcherAsyncListener : SimplePatchTool.IListener
	{
		private const int DEFAULT_REFRESH_INTERVAL = 100; // In milliseconds

		public delegate void LogReceiveDelegate( string log );
		public delegate void ProgressChangeDelegate( IOperationProgress progress );
		public delegate void PatchStageChangeDelegate( PatchStage stage );
		public delegate void PatchMethodChangeDelegate( PatchMethod method );
		public delegate void VersionInfoFetchDelegate( VersionInfo versionInfo );
		public delegate void VersionFetchDelegate( string currentVersion, string newVersion );
		public delegate void NoParameterDelegate();

		public event LogReceiveDelegate OnLogReceived;
		public event ProgressChangeDelegate OnProgressChanged;
		public event ProgressChangeDelegate OnOverallProgressChanged;
		public event PatchStageChangeDelegate OnPatchStageChanged;
		public event PatchMethodChangeDelegate OnPatchMethodChanged;
		public event VersionInfoFetchDelegate OnVersionInfoFetched;
		public event VersionFetchDelegate OnVersionFetched;
		public event NoParameterDelegate OnStart, OnFinish;

		public bool ReceiveLogs { get { return true; } }
		public bool ReceiveProgress { get { return true; } }

		public int RefreshInterval { get; set; }

		private string pendingLog;
		private IOperationProgress pendingProgress, pendingOverallProgress;
		private PatchStage? pendingStage;
		private PatchMethod? pendingMethod;
		private string pendingCurrentVersion, pendingNewVersion;

		private bool isThreadRunning;
		private bool isPatcherRunning;

		public PatcherAsyncListener()
		{
			RefreshInterval = DEFAULT_REFRESH_INTERVAL;

			pendingLog = null;
			pendingProgress = null;
			pendingOverallProgress = null;
			pendingStage = null;
			pendingMethod = null;
			pendingCurrentVersion = null;
			pendingNewVersion = null;

			isThreadRunning = false;
			isPatcherRunning = false;
		}

		void SimplePatchTool.IListener.Started()
		{
			isPatcherRunning = true;

			if( !isThreadRunning )
			{
				isThreadRunning = true;
				Initialize();
			}
		}

		void SimplePatchTool.IListener.LogReceived( string log )
		{
			pendingLog = log;
		}

		void SimplePatchTool.IListener.ProgressChanged( IOperationProgress progress )
		{
			pendingProgress = progress;
		}

		void SimplePatchTool.IListener.OverallProgressChanged( IOperationProgress progress )
		{
			pendingOverallProgress = progress;
		}

		void SimplePatchTool.IListener.PatchStageChanged( PatchStage stage )
		{
			pendingStage = stage;
		}

		void SimplePatchTool.IListener.PatchMethodChanged( PatchMethod method )
		{
			pendingMethod = method;
		}

		void SimplePatchTool.IListener.VersionInfoFetched( VersionInfo versionInfo )
		{
			// This is the only callback that blocks SimplePatchTool's thread because 
			// changes made to the VersionInfo should be committed before SimplePatchTool
			// starts patching (e.g. adding an ignored path to the VersionInfo)
			if( OnVersionInfoFetched != null )
				OnVersionInfoFetched( versionInfo );
		}

		void SimplePatchTool.IListener.VersionFetched( string currentVersion, string newVersion )
		{
			pendingCurrentVersion = currentVersion;
			pendingNewVersion = newVersion;
		}

		void SimplePatchTool.IListener.Finished()
		{
			isPatcherRunning = false;
		}

		protected virtual void Initialize()
		{
			PatchUtils.CreateBackgroundThread( new ThreadStart( ThreadRefresherFunction ) ).Start();
		}

		protected virtual void Terminate()
		{
		}

		protected virtual void Sleep()
		{
			Thread.Sleep( RefreshInterval );
		}

		private void ThreadRefresherFunction()
		{
			do
			{
				try
				{
					if( OnStart != null )
						OnStart();
				}
				catch { }

				while( isPatcherRunning )
				{
					try
					{
						Refresh();
						Sleep();
					}
					catch { }
				}

				try
				{
					Refresh();
				}
				catch { }

				try
				{
					if( OnFinish != null )
						OnFinish();
				}
				catch { }
			} while( isPatcherRunning ); // For example, this object might have been assigned to a new patcher in OnFinish

			isThreadRunning = false;
			Terminate();
		}

		protected void Refresh()
		{
			if( pendingLog != null )
			{
				if( OnLogReceived != null )
					OnLogReceived( pendingLog );

				pendingLog = null;
			}

			if( pendingProgress != null )
			{
				if( OnProgressChanged != null )
					OnProgressChanged( pendingProgress );

				pendingProgress = null;
			}

			if( pendingOverallProgress != null )
			{
				if( OnOverallProgressChanged != null )
					OnOverallProgressChanged( pendingOverallProgress );

				pendingOverallProgress = null;
			}

			if( pendingStage != null )
			{
				if( OnPatchStageChanged != null )
					OnPatchStageChanged( pendingStage.Value );

				pendingStage = null;
			}

			if( pendingMethod != null )
			{
				if( OnPatchMethodChanged != null )
					OnPatchMethodChanged( pendingMethod.Value );

				pendingMethod = null;
			}

			if( pendingCurrentVersion != null && pendingNewVersion != null )
			{
				if( OnVersionFetched != null )
					OnVersionFetched( pendingCurrentVersion, pendingNewVersion );

				pendingCurrentVersion = null;
				pendingNewVersion = null;
			}
		}
	}
}