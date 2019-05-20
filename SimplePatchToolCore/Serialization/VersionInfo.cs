using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace SimplePatchToolCore
{
	[XmlRoot( "VersionInfo" )]
	public class VersionInfo
	{
		private string m_name;
		public string Name
		{
			get { return m_name; }
			set
			{
				if( !PatchUtils.IsProjectNameValid( value ) )
					throw new FormatException( Localization.Get( StringId.E_XContainsInvalidCharacters, "'Name'" ) );

				m_name = value;
			}
		}

		public string BaseDownloadURL;
		public string MaintenanceCheckURL;

		public VersionCode Version;

		[XmlArray( ElementName = "Patches" )]
		public List<IncrementalPatch> IncrementalPatches;
		public InstallerPatch InstallerPatch;

		public List<string> IgnoredPaths;
		public List<VersionItem> Files;

		[XmlIgnore]
		public Regex[] IgnoredPathsRegex;

		public VersionInfo()
		{
			Name = "NewProject";
			Version = new VersionCode( 1, 0 );
			BaseDownloadURL = "";
			MaintenanceCheckURL = "";

			IncrementalPatches = new List<IncrementalPatch>();
			InstallerPatch = new InstallerPatch();

			IgnoredPaths = new List<string>();
			Files = new List<VersionItem>();
		}

		public string GetDownloadURLFor( VersionItem item )
		{
			if( !string.IsNullOrEmpty( item.DownloadURL ) )
				return item.DownloadURL;

			if( !string.IsNullOrEmpty( BaseDownloadURL ) )
				return BaseDownloadURL + PatchParameters.REPAIR_PATCH_DIRECTORY + '/' + item.Path.Replace( '\\', '/' ) + PatchParameters.REPAIR_PATCH_FILE_EXTENSION;

			return null;
		}

		public string GetDownloadURLFor( InstallerPatch patch )
		{
			if( !string.IsNullOrEmpty( patch.DownloadURL ) )
				return patch.DownloadURL;

			if( !string.IsNullOrEmpty( BaseDownloadURL ) )
				return BaseDownloadURL + PatchParameters.INSTALLER_PATCH_DIRECTORY + '/' + PatchParameters.INSTALLER_PATCH_FILENAME;

			return null;
		}

		public string GetDownloadURLFor( IncrementalPatch patch )
		{
			if( !string.IsNullOrEmpty( patch.DownloadURL ) )
				return patch.DownloadURL;

			if( !string.IsNullOrEmpty( BaseDownloadURL ) )
				return BaseDownloadURL + PatchParameters.INCREMENTAL_PATCH_DIRECTORY + '/' + patch.PatchVersion() + PatchParameters.INCREMENTAL_PATCH_FILE_EXTENSION;

			return null;
		}

		public string GetInfoURLFor( IncrementalPatch patch )
		{
			if( !string.IsNullOrEmpty( patch.InfoURL ) )
				return patch.InfoURL;

			if( !string.IsNullOrEmpty( BaseDownloadURL ) )
				return BaseDownloadURL + PatchParameters.INCREMENTAL_PATCH_DIRECTORY + '/' + patch.PatchVersion() + PatchParameters.INCREMENTAL_PATCH_INFO_EXTENSION;

			return null;
		}
	}

	public class VersionItem
	{
		[XmlAttribute]
		public string Path;

		[XmlAttribute]
		public long FileSize;

		[XmlAttribute]
		public string Md5Hash;

		[XmlAttribute]
		public long CompressedFileSize;

		[XmlAttribute]
		public string CompressedMd5Hash;

		[XmlAttribute]
		public string DownloadURL;

		public VersionItem()
		{
			Path = "";
			FileSize = 0L;
			Md5Hash = "";
			CompressedFileSize = 0L;
			CompressedMd5Hash = "";
			DownloadURL = "";
		}

		public VersionItem( string relativePath, FileInfo item )
		{
			Path = relativePath;
			FileSize = item.Length;
			Md5Hash = item.Md5Hash();
			CompressedFileSize = 0L;
			CompressedMd5Hash = "";
			DownloadURL = "";
		}

		public void OnCompressed( FileInfo compressedItem )
		{
			CompressedFileSize = compressedItem.Length;
			CompressedMd5Hash = compressedItem.Md5Hash();
		}
	}

	public class IncrementalPatch
	{
		public VersionCode FromVersion;
		public VersionCode ToVersion;

		[XmlAttribute]
		public int Files;

		[XmlAttribute]
		public long PatchSize;

		[XmlAttribute]
		public string PatchMd5Hash;

		[XmlAttribute]
		public string InfoURL;

		[XmlAttribute]
		public string DownloadURL;

		public IncrementalPatch()
		{
			FromVersion = new VersionCode( 0, 0 );
			ToVersion = new VersionCode( 0, 0 );
			Files = 0;
			PatchSize = 0L;
			PatchMd5Hash = "";
			InfoURL = "";
			DownloadURL = "";
		}

		public IncrementalPatch( VersionCode fromVersion, VersionCode toVersion, FileInfo patchFile, int numberOfFiles )
		{
			FromVersion = fromVersion;
			ToVersion = toVersion;
			Files = numberOfFiles;
			PatchSize = patchFile.Length;
			PatchMd5Hash = patchFile.Md5Hash();
			InfoURL = "";
			DownloadURL = "";
		}
	}

	public class InstallerPatch
	{
		[XmlAttribute]
		public long PatchSize;

		[XmlAttribute]
		public string PatchMd5Hash;

		[XmlAttribute]
		public string DownloadURL;

		public InstallerPatch()
		{
			PatchSize = 0L;
			PatchMd5Hash = "";
			DownloadURL = "";
		}

		public InstallerPatch( FileInfo patchFile )
		{
			PatchSize = patchFile.Length;
			PatchMd5Hash = patchFile.Md5Hash();
			DownloadURL = "";
		}
	}
}