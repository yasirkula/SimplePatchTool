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

			try
			{
				VersionInfo = PatchUtils.GetVersionInfoFromPath( versionInfoPath );
			}
			catch( Exception e )
			{
				if( logger != null )
					logger( e.ToString() );
			}

			if( VersionInfo == null )
				throw new InvalidOperationException( Localization.Get( StringId.E_VersionInfoCouldNotBeDeserializedFromX, versionInfoPath ) );

			this.versionInfoPath = versionInfoPath;
			Logger = logger;
		}

		public bool UpdateDownloadLinks( string downloadLinksPath )
		{
			string downloadLinksRaw;
			try
			{
				downloadLinksRaw = File.ReadAllText( downloadLinksPath );
			}
			catch( Exception e )
			{
				if( Logger != null )
				{
					Logger( e.ToString() );
					Logger( Localization.Get( StringId.E_CouldNotReadDownloadLinksFromX, downloadLinksPath ) );
				}

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
				if( key.IndexOf( PatchUtils.AltDirectorySeparatorChar ) >= 0 )
				{
					downloadLinks[key.Replace( PatchUtils.AltDirectorySeparatorChar, Path.DirectorySeparatorChar )] = downloadLinks[key];
					downloadLinks.Remove( key );
				}
			}

			int updateCount = 0;
			int totalCount = VersionInfo.Files.Count + VersionInfo.IncrementalPatches.Count;
			for( int i = 0; i < VersionInfo.Files.Count; i++ )
			{
				VersionItem item = VersionInfo.Files[i];

				string downloadLink;
				string relativePath = item.Path.Replace( PatchUtils.AltDirectorySeparatorChar, Path.DirectorySeparatorChar );
				if( downloadLinks.TryGetValue( relativePath, out downloadLink ) ||
					downloadLinks.TryGetValue( relativePath + PatchParameters.REPAIR_PATCH_FILE_EXTENSION, out downloadLink ) ||
					downloadLinks.TryGetValue( PatchParameters.REPAIR_PATCH_DIRECTORY + Path.DirectorySeparatorChar + relativePath, out downloadLink ) ||
					downloadLinks.TryGetValue( PatchParameters.REPAIR_PATCH_DIRECTORY + Path.DirectorySeparatorChar + relativePath + PatchParameters.REPAIR_PATCH_FILE_EXTENSION, out downloadLink ) )
				{
					item.DownloadURL = downloadLink;
					updateCount++;
				}
			}

			for( int i = 0; i < VersionInfo.IncrementalPatches.Count; i++ )
			{
				IncrementalPatch patch = VersionInfo.IncrementalPatches[i];

				string downloadLink;
				string relativePath = patch.PatchVersion() + PatchParameters.INCREMENTAL_PATCH_FILE_EXTENSION;
				if( downloadLinks.TryGetValue( relativePath, out downloadLink ) ||
					downloadLinks.TryGetValue( PatchParameters.INCREMENTAL_PATCH_DIRECTORY + Path.DirectorySeparatorChar + relativePath, out downloadLink ) )
				{
					patch.DownloadURL = downloadLink;
					updateCount++;
				}

				relativePath = patch.PatchVersion() + PatchParameters.INCREMENTAL_PATCH_INFO_EXTENSION;
				if( downloadLinks.TryGetValue( relativePath, out downloadLink ) ||
					downloadLinks.TryGetValue( PatchParameters.INCREMENTAL_PATCH_DIRECTORY + Path.DirectorySeparatorChar + relativePath, out downloadLink ) )
				{
					patch.InfoURL = downloadLink;
					updateCount++;
				}

				if( patch.Files > 0 )
					totalCount++;
			}

			if( VersionInfo.InstallerPatch.PatchSize > 0L || !string.IsNullOrEmpty( VersionInfo.InstallerPatch.PatchMd5Hash ) )
			{
				string downloadLink;
				string relativePath = PatchParameters.INSTALLER_PATCH_FILENAME;
				if( downloadLinks.TryGetValue( relativePath, out downloadLink ) ||
					downloadLinks.TryGetValue( PatchParameters.INSTALLER_PATCH_DIRECTORY + Path.DirectorySeparatorChar + relativePath, out downloadLink ) )
				{
					VersionInfo.InstallerPatch.DownloadURL = downloadLink;
					updateCount++;
				}

				totalCount++;
			}

			if( Logger != null )
				Logger( Localization.Get( StringId.XDownloadLinksAreUpdatedSuccessfully, updateCount, totalCount ) );

			return true;
		}

		public void SaveChanges()
		{
			PatchUtils.SerializeVersionInfoToXML( VersionInfo, versionInfoPath );
		}
	}
}