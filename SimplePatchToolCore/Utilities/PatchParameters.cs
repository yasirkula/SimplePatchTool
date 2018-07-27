namespace SimplePatchToolCore
{
	public static class PatchParameters
	{
		#region Constants
		public const string COMPRESSED_FILE_EXTENSION = ".lzdat";
		public const string PATCH_FILE_EXTENSION = ".patch";
		public const string PATCH_INFO_EXTENSION = ".info";

		public const string VERSION_INFO_FILENAME = "VersionInfo.info";
		public const string VERSION_HOLDER_FILENAME_POSTFIX = "_vers.sptv";

		public const string LOG_FILE_NAME = "logs.dat";
		public const long LOG_FILE_MAX_SIZE = 4 * 1024 * 1024L; // 4 MB

		public const string CACHE_DATE_HOLDER_FILENAME = "time.dat";
		public const int CACHE_DATE_EXPIRE_DAYS = 14;

		public const string SELF_PATCHER_DIRECTORY = "SPPatcher";

		public const string SELF_PATCH_INSTRUCTIONS_FILENAME = "psp.in0";
		public const string SELF_PATCH_COMPLETED_INSTRUCTIONS_FILENAME = "psp.in1";
		public const string SELF_PATCH_OP_SEPARATOR = "><";
		public const string SELF_PATCH_DELETE_OP = "_#DELETE#_";
		public const string SELF_PATCH_MOVE_OP = "_#MOVE#_";
		#endregion

		#region Parameters
		public static int FailedDownloadsRetryLimit = 3;
		public static long FailedDownloadsRetrySizeLimit = 30000000L; // ~30MB retry size limit per file
		public static int FailedDownloadsCooldownMillis = 5000;

		public static double DownloadStatsUpdateInterval = 0.2;

		public static int FileAvailabilityCheckTimeout = 8000;

		public static long FileHashCheckSizeLimit = long.MaxValue;
		#endregion
	}
}