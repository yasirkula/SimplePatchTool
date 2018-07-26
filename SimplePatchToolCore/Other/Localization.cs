using System.Collections.Generic;
using System.Globalization;

namespace SimplePatchToolCore
{
	public enum StringId
	{
		AllFilesAreDownloadedInXSeconds,
		AlreadyUpToDateXthFile,
		CalculatingDiffOfX,
		CalculatingFilesToDownload,
		CalculatingNewOrChangedFiles,
		CalculatingObsoleteFiles,
		Cancelled,
		CheckingIfFilesAreUpToDate,
		CompressingFilesToDestination,
		CompressingPatchIntoOneFile,
		CompressingXToY,
		CompressionFinishedInXSeconds,
		CompressionRatioIsX,
		CopyingXToPatch,
		CreatingIncrementalPatch,
		CreatingRepairPatch,
		CreatingXthFile,
		DecompressingPatchX,
		DeletingX,
		DeletingXObsoleteFiles,
		Done,
		DownloadingPatchX,
		DownloadingXFiles,
		DownloadingXProgressInfo,
		DownloadingXthFile,
		E_AccessToXIsForbiddenRunInAdminMode,
		E_CouldNotDownloadPatchInfoX,
		E_CouldNotReadDownloadLinksFromX,
		E_DiffOfXDoesNotExist,
		E_DirectoryXIsNotEmpty,
		E_DownloadedFileXIsCorrupt,
		E_DownloadLinkXIsNotValid,
		E_FilesAreNotUpToDateAfterPatch,
		E_FileXDoesNotExistOnServer,
		E_FileXIsNotValidOnServer,
		E_InsufficientSpaceXNeededInY,
		E_InvalidPatchInfoX,
		E_NoSuitablePatchMethodFound,
		E_PatchInfoCouldNotBeVerified,
		E_PatchInfoDoesNotExistAtX,
		E_PatchXCouldNotBeDownloaded,
		E_PreviousVersionXIsNotLessThanY,
		E_ProjectNameContainsInvalidCharacters,
		E_SelfPatcherDoesNotExist,
		E_ServersUnderMaintenance,
		E_VersionCodeXIsInvalid,
		E_VersionInfoCouldNotBeDeserializedFromX,
		E_VersionInfoCouldNotBeDownloaded,
		E_VersionInfoCouldNotBeVerified,
		E_VersionInfoInvalid,
		E_XCanNotBeEmpty,
		E_XCouldNotBeDownloaded,
		E_XDoesNotExist,
		GeneratingListOfFilesInBuild,
		IncrementalPatchCreatedInXSeconds,
		NoObsoleteFiles,
		PatchAppliedInXSeconds,
		PatchCompletedInXSeconds,
		PatchCreatedInXSeconds,
		RenamingXFiles,
		RetrievingVersionInfo,
		SomeFilesAreStillNotUpToDate,
		UpdatingX,
		UpdatingXFiles,
		UpdatingXFilesAtY,
		UpdatingXthFile,
		WritingIncrementalPatchInfoToXML,
		WritingVersionInfoToXML,
		XDownloadedInYSeconds,
		XDownloadLinksAreUpdatedSuccessfully,
		XFilesUpdatedSuccessfully
	}

	public static class Localization
	{
		// A custom IEqualityComparer to avoid GC for using enum as key to dictionary
		public class StringIdComparer : IEqualityComparer<StringId>
		{
			public bool Equals( StringId s1, StringId s2 ) { return s1 == s2; }
			public int GetHashCode( StringId s ) { return (int) s; }
		}

		private static Dictionary<StringId, string> Strings;
		public static string CurrentLanguageISOCode { get; private set; }

		static Localization()
		{
			Strings = new Dictionary<StringId, string>( new StringIdComparer() );
			SetCulture( CultureInfo.CurrentCulture );
		}

		public static string Get( StringId key )
		{
			string result;
			if( Strings.TryGetValue( key, out result ) )
				return result;

			return key.ToString();
		}

		public static string Get( StringId key, object arg0 )
		{
			string result;
			if( Strings.TryGetValue( key, out result ) )
				return string.Format( result, arg0 );

			return key.ToString();
		}

		public static string Get( StringId key, object arg0, object arg1 )
		{
			string result;
			if( Strings.TryGetValue( key, out result ) )
				return string.Format( result, arg0, arg1 );

			return key.ToString();
		}

		public static string Get( StringId key, object arg0, object arg1, object arg2 )
		{
			string result;
			if( Strings.TryGetValue( key, out result ) )
				return string.Format( result, arg0, arg1, arg2 );

			return key.ToString();
		}

		public static string Get( StringId key, params object[] args )
		{
			string result;
			if( Strings.TryGetValue( key, out result ) )
				return string.Format( result, args );

			return key.ToString();
		}

		public static bool SetCulture( CultureInfo culture )
		{
			return SetLanguage( culture.TwoLetterISOLanguageName );
		}

		public static bool SetLanguage( string languageISOCode )
		{
			if( string.IsNullOrEmpty( languageISOCode ) )
				return false;

			languageISOCode = languageISOCode.ToLowerInvariant();
			if( CurrentLanguageISOCode == languageISOCode )
				return true;

			CurrentLanguageISOCode = languageISOCode;
			if( languageISOCode == "en" )
				SetLanguageEN();
			else if( languageISOCode == "tr" )
				SetLanguageTR();
			else
			{
				SetLanguage( "en" );
				return false;
			}

			return true;
		}

		public static bool SetStrings( Dictionary<StringId, string> strings, string languageISOCode = null )
		{
			if( strings != null && strings.Count > 0 )
			{
				Strings = strings;
				if( !string.IsNullOrEmpty( languageISOCode ) )
					CurrentLanguageISOCode = languageISOCode.ToLowerInvariant();
				else
					CurrentLanguageISOCode = null;

				return true;
			}

			return false;
		}

		private static void SetLanguageEN()
		{
			Strings.Clear();

			Strings[StringId.AllFilesAreDownloadedInXSeconds] = "All files are successfully downloaded in {0} seconds";
			Strings[StringId.AlreadyUpToDateXthFile] = "{0}/{1} Already up-to-date: {2}";
			Strings[StringId.CalculatingDiffOfX] = "Calculating diff of {0}";
			Strings[StringId.CalculatingFilesToDownload] = "...Calculating files to download...";
			Strings[StringId.CalculatingNewOrChangedFiles] = "...Calculating new or changed files...";
			Strings[StringId.CalculatingObsoleteFiles] = "...Calculating obsolete files...";
			Strings[StringId.Cancelled] = "...Operation cancelled...";
			Strings[StringId.CheckingIfFilesAreUpToDate] = "...Checking if files are up-to-date...";
			Strings[StringId.CompressingFilesToDestination] = "...Compressing files in build to destination...";
			Strings[StringId.CompressingPatchIntoOneFile] = "...Compressing incremental patch into one file...";
			Strings[StringId.CompressingXToY] = "Compressing {0} to {1}";
			Strings[StringId.CompressionFinishedInXSeconds] = "Compression finished in {0} seconds";
			Strings[StringId.CompressionRatioIsX] = "Compression ratio is {0}%";
			Strings[StringId.CopyingXToPatch] = "Copying {0} to patch";
			Strings[StringId.CreatingIncrementalPatch] = "...Creating incremental patch...";
			Strings[StringId.CreatingRepairPatch] = "...Creating repair patch...";
			Strings[StringId.CreatingXthFile] = "{0}/{1} Creating: {2}";
			Strings[StringId.DecompressingPatchX] = "...Decompressing patch {0}...";
			Strings[StringId.DeletingX] = "Deleting {0}";
			Strings[StringId.DeletingXObsoleteFiles] = "...Deleting {0} obsolete files...";
			Strings[StringId.Done] = "...Done...";
			Strings[StringId.DownloadingPatchX] = "...Downloading patch: {0}...";
			Strings[StringId.DownloadingXFiles] = "...Downloading {0} new or updated file(s)...";
			Strings[StringId.DownloadingXProgressInfo] = "Downloading {0}: {1}/{2}MB ({3})";
			Strings[StringId.DownloadingXthFile] = "{0}/{1} Downloading: {2} ({3}MB)";
			Strings[StringId.E_AccessToXIsForbiddenRunInAdminMode] = "ERROR: access to {0} is forbidden; run patcher in administrator mode";
			Strings[StringId.E_CouldNotDownloadPatchInfoX] = "ERROR: could not download patch info for {0}";
			Strings[StringId.E_CouldNotReadDownloadLinksFromX] = "ERROR: could not read download links from {0}";
			Strings[StringId.E_DiffOfXDoesNotExist] = "ERROR: patch file for {0} couldn't be found";
			Strings[StringId.E_DirectoryXIsNotEmpty] = "ERROR: directory {0} is not empty";
			Strings[StringId.E_DownloadedFileXIsCorrupt] = "ERROR: downloaded file {0} is corrupt";
			Strings[StringId.E_DownloadLinkXIsNotValid] = "ERROR: download link {0} is not in form [RelativePathToFile URL]";
			Strings[StringId.E_FilesAreNotUpToDateAfterPatch] = "ERROR: files are not up-to-date after the patch";
			Strings[StringId.E_FileXDoesNotExistOnServer] = "ERROR: file {0} does not exist on server";
			Strings[StringId.E_FileXIsNotValidOnServer] = "ERROR: file {0} is not valid on server";
			Strings[StringId.E_InsufficientSpaceXNeededInY] = "ERROR: insufficient free space in {1}, at least {0} needed";
			Strings[StringId.E_InvalidPatchInfoX] = "ERROR: patch info for {0} is invalid";
			Strings[StringId.E_NoSuitablePatchMethodFound] = "ERROR: no suitable patch method found";
			Strings[StringId.E_PatchInfoCouldNotBeVerified] = "ERROR: could not verify downloaded patch info";
			Strings[StringId.E_PatchInfoDoesNotExistAtX] = "ERROR: patch info does not exist at {0}";
			Strings[StringId.E_PatchXCouldNotBeDownloaded] = "ERROR: patch {0} could not be downloaded";
			Strings[StringId.E_PreviousVersionXIsNotLessThanY] = "ERROR: previous version ({0}) is greater than or equal to current version ({1})";
			Strings[StringId.E_ProjectNameContainsInvalidCharacters] = "ERROR: 'projectName' contains invalid character(s)";
			Strings[StringId.E_SelfPatcherDoesNotExist] = "ERROR: self patcher does not exist";
			Strings[StringId.E_ServersUnderMaintenance] = "ERROR: servers are currently under maintenance";
			Strings[StringId.E_VersionCodeXIsInvalid] = "ERROR: version code '{0}' is invalid";
			Strings[StringId.E_VersionInfoCouldNotBeDeserializedFromX] = "ERROR: version info could not be deserialized from {0}";
			Strings[StringId.E_VersionInfoCouldNotBeDownloaded] = "ERROR: could not download version info from server";
			Strings[StringId.E_VersionInfoCouldNotBeVerified] = "ERROR: could not verify downloaded version info";
			Strings[StringId.E_VersionInfoInvalid] = "ERROR: version info is invalid";
			Strings[StringId.E_XCanNotBeEmpty] = "ERROR: {0} can not be empty";
			Strings[StringId.E_XCouldNotBeDownloaded] = "ERROR: {0} could not be downloaded";
			Strings[StringId.E_XDoesNotExist] = "ERROR: {0} does not exist";
			Strings[StringId.GeneratingListOfFilesInBuild] = "...Generating list of files in the build...";
			Strings[StringId.IncrementalPatchCreatedInXSeconds] = "...Incremental patch created in {0} seconds...";
			Strings[StringId.NoObsoleteFiles] = "...No obsolete files...";
			Strings[StringId.PatchAppliedInXSeconds] = "...Patch applied in {0} seconds...";
			Strings[StringId.PatchCompletedInXSeconds] = "...Patch successfully completed in {0} seconds...";
			Strings[StringId.PatchCreatedInXSeconds] = "...Patch created in {0} seconds...";
			Strings[StringId.RenamingXFiles] = "...Renaming {0} files/folders";
			Strings[StringId.RetrievingVersionInfo] = "...Retrieving version info...";
			Strings[StringId.SomeFilesAreStillNotUpToDate] = "...Some files are still not up-to-date, trying repair...";
			Strings[StringId.UpdatingX] = "Updating {0}";
			Strings[StringId.UpdatingXFiles] = "...Updating {0} file(s)...";
			Strings[StringId.UpdatingXFilesAtY] = "...Updating {0} file(s) at {1}...";
			Strings[StringId.UpdatingXthFile] = "{0}/{1} Updating: {2}";
			Strings[StringId.WritingIncrementalPatchInfoToXML] = "...Writing incremental patch info to XML...";
			Strings[StringId.WritingVersionInfoToXML] = "...Writing version info to XML...";
			Strings[StringId.XDownloadedInYSeconds] = "{0} downloaded in {1} seconds";
			Strings[StringId.XDownloadLinksAreUpdatedSuccessfully] = "{0}/{1} download links are updated successfully";
			Strings[StringId.XFilesUpdatedSuccessfully] = "{0}/{1} files updated successfully";
		}

		private static void SetLanguageTR()
		{
			Strings.Clear();

			Strings[StringId.AllFilesAreDownloadedInXSeconds] = "Tüm dosyalar {0} saniyede başarılı bir şekilde indirildi";
			Strings[StringId.AlreadyUpToDateXthFile] = "{0}/{1} Zaten güncel: {2}";
			Strings[StringId.CalculatingDiffOfX] = "{0} dosyasının diff'i hesaplanıyor";
			Strings[StringId.CalculatingFilesToDownload] = "...İndirilmesi gereken dosyalar hesaplanıyor...";
			Strings[StringId.CalculatingNewOrChangedFiles] = "...Yeni veya değişmiş dosyalar hesaplanıyor...";
			Strings[StringId.CalculatingObsoleteFiles] = "...Artık kullanılmayan dosyalar hesaplanıyor...";
			Strings[StringId.Cancelled] = "...İşlem iptal edildi...";
			Strings[StringId.CheckingIfFilesAreUpToDate] = "...Dosyaların güncel olup olmadığı kontrol ediliyor...";
			Strings[StringId.CompressingFilesToDestination] = "...Dosyalar hedef klasörde sıkıştırılıyor...";
			Strings[StringId.CompressingPatchIntoOneFile] = "...Patch dosyası sıkıştırılıyor...";
			Strings[StringId.CompressingXToY] = "{0} sıkıştırılıyor: {1}";
			Strings[StringId.CompressionFinishedInXSeconds] = "Sıkıştırma işlemi {0} saniyede tamamlandı";
			Strings[StringId.CompressionRatioIsX] = "Toplam sıkıştırma oranı: %{0}";
			Strings[StringId.CopyingXToPatch] = "{0} patch'in içerisine kopyalanıyor";
			Strings[StringId.CreatingIncrementalPatch] = "...Incremental patch oluşturuluyor...";
			Strings[StringId.CreatingRepairPatch] = "...Repair patch oluşturuluyor...";
			Strings[StringId.CreatingXthFile] = "{0}/{1} Oluşturuluyor: {2}";
			Strings[StringId.DecompressingPatchX] = "...Patch'in içerisindeki dosyalar çıkartılıyor: {0}...";
			Strings[StringId.DeletingX] = "Siliniyor: {0}";
			Strings[StringId.DeletingXObsoleteFiles] = "...{0} eski dosya siliniyor...";
			Strings[StringId.Done] = "...Tamamlandı...";
			Strings[StringId.DownloadingPatchX] = "...Patch indiriliyor: {0}...";
			Strings[StringId.DownloadingXFiles] = "...{0} yeni veya değişmiş dosya indiriliyor...";
			Strings[StringId.DownloadingXProgressInfo] = "{0} indiriliyor: {1}/{2}MB ({3})";
			Strings[StringId.DownloadingXthFile] = "{0}/{1} İndiriliyor: {2} ({3}MB)";
			Strings[StringId.E_AccessToXIsForbiddenRunInAdminMode] = "HATA: {0} konumuna erişim engellendi, uygulamayı yönetici olarak çalıştırın";
			Strings[StringId.E_FilesAreNotUpToDateAfterPatch] = "HATA: patch sonrası dosyalar hâlâ güncel değil";
			Strings[StringId.E_CouldNotDownloadPatchInfoX] = "HATA: {0} için patch bilgileri indirilemiyor";
			Strings[StringId.E_CouldNotReadDownloadLinksFromX] = "HATA: indirme linkleri {0} dosyasından okunamadı";
			Strings[StringId.E_DiffOfXDoesNotExist] = "HATA: {0} için patch dosyası bulunamadı";
			Strings[StringId.E_DirectoryXIsNotEmpty] = "HATA: klasörün içi boş değil: {0}";
			Strings[StringId.E_DownloadedFileXIsCorrupt] = "HATA: indirilen dosya {0} bozuk";
			Strings[StringId.E_DownloadLinkXIsNotValid] = "HATA: {0} şu formda değil: [DosyanınKonumu İndirmeLinki]";
			Strings[StringId.E_FileXDoesNotExistOnServer] = "HATA: {0} dosyası sunucuda bulunamadı";
			Strings[StringId.E_FileXIsNotValidOnServer] = "HATA: sunucudaki {0} dosyası bozuk";
			Strings[StringId.E_InsufficientSpaceXNeededInY] = "HATA: {1} diskinde yeterli boş yer yok, en az {0} gerekli";
			Strings[StringId.E_InvalidPatchInfoX] = "HATA: {0} için patch bilgileri hatalı";
			Strings[StringId.E_NoSuitablePatchMethodFound] = "HATA: uygulanacak uygun bir patch yöntemi bulunamadı";
			Strings[StringId.E_PatchInfoCouldNotBeVerified] = "HATA: indirilen patch bilgileri doğrulanamıyor";
			Strings[StringId.E_PatchInfoDoesNotExistAtX] = "HATA: patch bilgileri {0} konumunda bulunamadı";
			Strings[StringId.E_PatchXCouldNotBeDownloaded] = "HATA: patch {0} indirilemedi";
			Strings[StringId.E_PreviousVersionXIsNotLessThanY] = "HATA: önceki sürümün versiyonu ({0}) mevcut versiyona ({1}) eşit veya daha büyük";
			Strings[StringId.E_ProjectNameContainsInvalidCharacters] = "HATA: 'projectName' geçersiz karakter(ler) içermekte";
			Strings[StringId.E_SelfPatcherDoesNotExist] = "HATA: oto-patch dosyası mevcut değil";
			Strings[StringId.E_ServersUnderMaintenance] = "HATA: sunucular şu anda bakım modunda";
			Strings[StringId.E_VersionCodeXIsInvalid] = "HATA: versiyon kodu '{0}' geçersiz";
			Strings[StringId.E_VersionInfoCouldNotBeDeserializedFromX] = "HATA: {0} konumundaki versiyon bilgileri bozuk";
			Strings[StringId.E_VersionInfoCouldNotBeDownloaded] = "HATA: versiyon bilgileri sunucudan çekilemiyor";
			Strings[StringId.E_VersionInfoCouldNotBeVerified] = "HATA: indirilen versiyon bilgileri doğrulanamıyor";
			Strings[StringId.E_VersionInfoInvalid] = "HATA: versiyon bilgileri hatalı";
			Strings[StringId.E_XCanNotBeEmpty] = "HATA: {0} boş bırakılamaz";
			Strings[StringId.E_XCouldNotBeDownloaded] = "HATA: {0} indirilemedi";
			Strings[StringId.E_XDoesNotExist] = "HATA: {0} bulunamadı";
			Strings[StringId.GeneratingListOfFilesInBuild] = "...Versiyondaki dosyaların listesi çıkartılıyor...";
			Strings[StringId.IncrementalPatchCreatedInXSeconds] = "...Incremental patch {0} saniyede oluşturuldu...";
			Strings[StringId.NoObsoleteFiles] = "...Eski dosya bulunmamakta...";
			Strings[StringId.PatchAppliedInXSeconds] = "...Patch {0} saniyede tamamlandı...";
			Strings[StringId.PatchCompletedInXSeconds] = "...Patch {0} saniyede başarıyla tamamlandı...";
			Strings[StringId.PatchCreatedInXSeconds] = "...Patch {0} saniyede oluşturuldu...";
			Strings[StringId.RenamingXFiles] = "...{0} dosya veya klasörün ismi güncelleniyor";
			Strings[StringId.RetrievingVersionInfo] = "...Versiyon bilgileri alınıyor...";
			Strings[StringId.SomeFilesAreStillNotUpToDate] = "...Bazı dosyalar hâlâ güncel değil, onarma işlemi deneniyor...";
			Strings[StringId.UpdatingX] = "{0} güncelleniyor";
			Strings[StringId.UpdatingXFiles] = "...{0} dosya güncelleniyor...";
			Strings[StringId.UpdatingXFilesAtY] = "...{1} konumundaki {0} dosya güncelleniyor...";
			Strings[StringId.UpdatingXthFile] = "{0}/{1} Güncelleniyor: {2}";
			Strings[StringId.WritingIncrementalPatchInfoToXML] = "...Incremental patch bilgileri XML dosyasına yazılıyor...";
			Strings[StringId.WritingVersionInfoToXML] = "...Versiyon bilgileri XML dosyasına yazılıyor...";
			Strings[StringId.XDownloadedInYSeconds] = "{0} {1} saniyede indirildi";
			Strings[StringId.XDownloadLinksAreUpdatedSuccessfully] = "{0}/{1} indirme linki başarıyla güncellendi";
			Strings[StringId.XFilesUpdatedSuccessfully] = "{0}/{1} dosya başarıyla güncellendi";
		}
	}
}