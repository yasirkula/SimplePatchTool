using System;
using System.Collections.Generic;
using System.IO;

namespace SimplePatchToolCore
{
	public class PatchUpdater
	{
		private readonly string versionInfoPath;
		public VersionInfo VersionInfo { get; private set; }

		public delegate void LogEvent( string log );
		private readonly LogEvent Logger;

		/// <exception cref = "FileNotFoundException">Version info does not exist at path</exception>
		/// <exception cref = "ArgumentException">versionInfoPath is empty</exception>
		/// <exception cref = "UnauthorizedAccessException">Admin priviledges needed to update the version info</exception>
		/// <exception cref = "InvalidOperationException">Version info could not be deserialized</exception>
		public PatchUpdater( string versionInfoPath, LogEvent logger = null )
		{
			versionInfoPath = versionInfoPath.Trim();
			if( string.IsNullOrEmpty( versionInfoPath ) )
				throw new ArgumentException( Localization.Get( StringId.E_XCanNotBeEmpty, "'versionInfoPath'" ) );

			if( !File.Exists( versionInfoPath ) )
				throw new FileNotFoundException( Localization.Get( StringId.E_PatchInfoDoesNotExistAtX, versionInfoPath ) );

			if( !PatchUtils.CheckWriteAccessToFolder( Path.GetDirectoryName( versionInfoPath ) ) )
				throw new UnauthorizedAccessException( Localization.Get( StringId.E_AccessToXIsForbiddenRunInAdminMode, versionInfoPath ) );

			this.versionInfoPath = versionInfoPath;
			VersionInfo = PatchUtils.GetVersionInfoFromPath( versionInfoPath );
			if( VersionInfo == null )
				throw new InvalidOperationException( Localization.Get( StringId.E_VersionInfoCouldNotBeDeserializedFromX, versionInfoPath ) );

			Logger = logger;
		}

		public bool UpdateDownloadLinks( string downloadLinksPath )
		{
			string downloadLinksRaw;
			try
			{
				downloadLinksRaw = File.ReadAllText( downloadLinksPath );
			}
			catch
			{
				if( Logger != null )
					Logger( Localization.Get( StringId.E_CouldNotReadDownloadLinksFromX, downloadLinksPath ) );

				return false;
			}

			Dictionary<string, string> downloadLinks = new Dictionary<string, string>();

			string[] downloadLinksSplit = downloadLinksRaw.Replace( "\r", "" ).Split( '\n' );
			for( int i = 0; i < downloadLinksSplit.Length; i++ )
			{
				string downloadLinkRaw = downloadLinksSplit[i].Trim();
				if( string.IsNullOrEmpty( downloadLinkRaw ) )
					continue;

				int separatorIndex = downloadLinkRaw.LastIndexOf( ' ' );
				if( separatorIndex == -1 )
				{
					if( Logger != null )
						Logger( Localization.Get( StringId.E_DownloadLinkXIsNotValid, downloadLinkRaw ) );

					continue;
				}

				downloadLinks[downloadLinkRaw.Substring( 0, separatorIndex )] = downloadLinkRaw.Substring( separatorIndex + 1 );
			}

			return UpdateDownloadLinks( downloadLinks );
		}

		public bool UpdateDownloadLinks( Dictionary<string, string> downloadLinks )
		{
			// Replace all AltDirectorySeparatorChar's with DirectorySeparatorChar's in the dictionary keys
			List<string> keys = new List<string>( downloadLinks.Keys );
			for( int i = 0; i < keys.Count; i++ )
			{
				string key = keys[i];
				if( key.IndexOf( Path.AltDirectorySeparatorChar ) >= 0 )
				{
					downloadLinks[key.Replace( Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar )] = downloadLinks[key];
					downloadLinks.Remove( key );
				}
			}

			int updateCount = 0;
			for( int i = 0; i < VersionInfo.Files.Count; i++ )
			{
				VersionItem item = VersionInfo.Files[i];

				string downloadLink;
				if( downloadLinks.TryGetValue( item.Path.Replace( Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar ), out downloadLink ) ||
					downloadLinks.TryGetValue( item.Path.Replace( Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar ) + PatchParameters.COMPRESSED_FILE_EXTENSION, out downloadLink ) )
				{
					item.DownloadURL = downloadLink;
					updateCount++;
				}
			}

			for( int i = 0; i < VersionInfo.Patches.Count; i++ )
			{
				IncrementalPatch patch = VersionInfo.Patches[i];

				string downloadLink;
				if( downloadLinks.TryGetValue( patch.PatchVersion() + PatchParameters.PATCH_FILE_EXTENSION, out downloadLink ) )
				{
					patch.DownloadURL = downloadLink;
					updateCount++;
				}

				if( downloadLinks.TryGetValue( patch.PatchVersion() + PatchParameters.PATCH_INFO_EXTENSION, out downloadLink ) )
				{
					patch.InfoURL = downloadLink;
					updateCount++;
				}
			}

			if( Logger != null )
				Logger( Localization.Get( StringId.XDownloadLinksAreUpdatedSuccessfully, updateCount, updateCount <= VersionInfo.Files.Count ? VersionInfo.Files.Count : updateCount ) );

			return true;
		}

		public void SaveChanges()
		{
			PatchUtils.SerializeVersionInfoToXML( VersionInfo, versionInfoPath );
		}
	}
}