namespace SimplePatchToolCore
{
	public static class PatchParameters
	{
		#region Constants
		public const string REPAIR_PATCH_FILE_EXTENSION = ".lzdat";
		public const string INCREMENTAL_PATCH_FILE_EXTENSION = ".patch";
		public const string INCREMENTAL_PATCH_INFO_EXTENSION = ".info";
		public const string INSTALLER_PATCH_FILENAME = "Installer.patch";

		public const string VERSION_INFO_FILENAME = "VersionInfo.info";
		public const string VERSION_HOLDER_FILENAME_POSTFIX = "_vers.sptv";

		public const string REPAIR_PATCH_DIRECTORY = "RepairPatch";
		public const string INCREMENTAL_PATCH_DIRECTORY = "IncrementalPatch";
		public const string INSTALLER_PATCH_DIRECTORY = "InstallerPatch";

		public const string PROJECT_VERSIONS_DIRECTORY = "Versions";
		public const string PROJECT_SELF_PATCHER_DIRECTORY = "SelfPatcher";
		public const string PROJECT_OUTPUT_DIRECTORY = "PatchFiles";
		public const string PROJECT_RSA_KEYS_DIRECTORY = "RSA";
		public const string PROJECT_SETTINGS_FILENAME = "Settings.xml";
		public const string PROJECT_RSA_PUBLIC_FILENAME = "public.key";
		public const string PROJECT_RSA_PRIVATE_FILENAME = "private.key";

		public const string LOG_FILE_NAME = "spt_logs.txt";
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