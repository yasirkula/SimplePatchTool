using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;

namespace SimplePatchToolCore
{
	public static class PatchUtils
	{
		#region Constants
		private const double BYTES_TO_KB_MULTIPLIER = 1.0 / 1024;
		private const double BYTES_TO_MB_MULTIPLIER = 1.0 / ( 1024 * 1024 );
		#endregion

		#region Properties
		private static char m_altDirectorySeparatorChar = '\0';
		public static char AltDirectorySeparatorChar
		{
			get
			{
				if( m_altDirectorySeparatorChar == '\0' )
				{
					if( Path.DirectorySeparatorChar != Path.AltDirectorySeparatorChar )
						m_altDirectorySeparatorChar = Path.AltDirectorySeparatorChar;
					else if( Path.DirectorySeparatorChar == '\\' )
						m_altDirectorySeparatorChar = '/';
					else
						m_altDirectorySeparatorChar = '\\';
				}

				return m_altDirectorySeparatorChar;
			}
		}
		#endregion

		#region Extension Functions
		public static string ElapsedSeconds( this Stopwatch timer )
		{
			return ( timer.ElapsedMilliseconds / 1000.0 ).ToString( "F3" );
		}

		public static string ToKilobytes( this long bytes )
		{
			return ( bytes * BYTES_TO_KB_MULTIPLIER ).ToString( "F2" );
		}

		public static string ToMegabytes( this long bytes )
		{
			return ( bytes * BYTES_TO_MB_MULTIPLIER ).ToString( "F2" );
		}

		public static string Md5Hash( this FileInfo fileInfo )
		{
			using( var md5 = MD5.Create() )
			{
				using( var stream = File.OpenRead( fileInfo.FullName ) )
				{
					return BitConverter.ToString( md5.ComputeHash( stream ) ).Replace( "-", "" ).ToLowerInvariant();
				}
			}
		}

		public static bool MatchesSignature( this FileInfo fileInfo, long fileSize, string md5 )
		{
			if( fileInfo.Length == fileSize )
				return fileSize > PatchParameters.FileHashCheckSizeLimit || fileInfo.Md5Hash() == md5;

			return false;
		}

		public static bool MatchesSignature( this FileInfo fileInfo, FileInfo other )
		{
			long fileSize = other.Length;
			if( fileInfo.Length == fileSize )
				return fileSize > PatchParameters.FileHashCheckSizeLimit || fileInfo.Md5Hash() == other.Md5Hash();

			return false;
		}

		public static bool PathMatchesPattern( this List<Regex> patterns, string path )
		{
			for( int i = 0; i < patterns.Count; i++ )
			{
				if( patterns[i].IsMatch( path ) )
					return true;
			}

			return false;
		}

		public static string PatchVersionBrief( this IncrementalPatch incrementalPatch )
		{
			return incrementalPatch.FromVersion + "_" + incrementalPatch.ToVersion;
		}

		public static string PatchVersion( this IncrementalPatch incrementalPatch )
		{
			return incrementalPatch.FromVersion.ToString().Replace( '.', '_' ) + "__" + incrementalPatch.ToVersion.ToString().Replace( '.', '_' );
		}

		public static string PatchVersion( this IncrementalPatchInfo patchInfo )
		{
			return patchInfo.FromVersion.ToString().Replace( '.', '_' ) + "__" + patchInfo.ToVersion.ToString().Replace( '.', '_' );
		}

		/// <summary>
		/// Adds an ignored path to VersionInfo (runtime only)
		/// </summary>
		public static void AddIgnoredPath( this VersionInfo versionInfo, string ignoredPath )
		{
			versionInfo.IgnoredPathsRegex.Add( WildcardToRegex( ignoredPath.Replace( AltDirectorySeparatorChar, Path.DirectorySeparatorChar ) ) );
		}
		#endregion

		#region XML Functions
		public static void SerializeVersionInfoToXML( VersionInfo version, string xmlPath )
		{
			var serializer = new XmlSerializer( typeof( VersionInfo ) );
			using( var stream = new FileStream( xmlPath, FileMode.Create ) )
			{
				serializer.Serialize( stream, version );
			}
		}

		public static VersionInfo DeserializeXMLToVersionInfo( string xmlContent )
		{
			var serializer = new XmlSerializer( typeof( VersionInfo ) );
			using( var stream = new StringReader( xmlContent ) )
			{
				VersionInfo result = serializer.Deserialize( stream ) as VersionInfo;
				if( result != null )
				{
					result.IncrementalPatches.RemoveAll( ( patch ) => !patch.FromVersion.IsValid || !patch.ToVersion.IsValid || patch.ToVersion <= patch.FromVersion );
					result.IncrementalPatches.Sort( IncrementalPatchComparison );

					// BaseDownloadURL uses '/' as path separator char, be consistent
					if( result.BaseDownloadURL.StartsWith( "file://" ) )
						result.BaseDownloadURL = "file://" + result.BaseDownloadURL.Substring( 7 ).Replace( '\\', '/' );

					// Always use Path.DirectorySeparatorChar
					for( int i = 0; i < result.Files.Count; i++ )
					{
						VersionItem item = result.Files[i];
						item.Path = item.Path.Replace( AltDirectorySeparatorChar, Path.DirectorySeparatorChar );
					}

					result.IgnoredPathsRegex = new List<Regex>( result.IgnoredPaths.Count + 2 );
					for( int i = 0; i < result.IgnoredPaths.Count; i++ )
						result.IgnoredPathsRegex.Add( WildcardToRegex( result.IgnoredPaths[i].Replace( AltDirectorySeparatorChar, Path.DirectorySeparatorChar ) ) );

					result.IgnoredPathsRegex.Add( WildcardToRegex( "*" + PatchParameters.VERSION_HOLDER_FILENAME_POSTFIX ) );
					result.IgnoredPathsRegex.Add( WildcardToRegex( "*" + PatchParameters.LOG_FILE_NAME ) );
				}

				return result;
			}
		}

		public static void SerializeIncrementalPatchInfoToXML( IncrementalPatchInfo patch, string xmlPath )
		{
			var serializer = new XmlSerializer( typeof( IncrementalPatchInfo ) );
			using( var stream = new FileStream( xmlPath, FileMode.Create ) )
			{
				serializer.Serialize( stream, patch );
			}
		}

		public static IncrementalPatchInfo DeserializeXMLToIncrementalPatchInfo( string xmlContent )
		{
			var serializer = new XmlSerializer( typeof( IncrementalPatchInfo ) );
			using( var stream = new StringReader( xmlContent ) )
			{
				IncrementalPatchInfo result = serializer.Deserialize( stream ) as IncrementalPatchInfo;
				if( result != null )
				{
					// Always use Path.DirectorySeparatorChar
					for( int i = 0; i < result.RenamedFiles.Count; i++ )
					{
						PatchRenamedItem renamedItem = result.RenamedFiles[i];
						renamedItem.BeforePath = renamedItem.BeforePath.Replace( AltDirectorySeparatorChar, Path.DirectorySeparatorChar );
						renamedItem.AfterPath = renamedItem.AfterPath.Replace( AltDirectorySeparatorChar, Path.DirectorySeparatorChar );
					}

					for( int i = 0; i < result.Files.Count; i++ )
					{
						PatchItem patchItem = result.Files[i];
						patchItem.Path = patchItem.Path.Replace( AltDirectorySeparatorChar, Path.DirectorySeparatorChar );
					}
				}

				return result;
			}
		}

		public static void SerializeProjectInfoToXML( ProjectInfo project, string xmlPath )
		{
			var serializer = new XmlSerializer( typeof( ProjectInfo ) );
			using( var stream = new FileStream( xmlPath, FileMode.Create ) )
			{
				serializer.Serialize( stream, project );
			}
		}

		public static ProjectInfo DeserializeXMLToProjectInfo( string xmlContent )
		{
			var serializer = new XmlSerializer( typeof( ProjectInfo ) );
			using( var stream = new StringReader( xmlContent ) )
			{
				return serializer.Deserialize( stream ) as ProjectInfo;
			}
		}

		internal static int IncrementalPatchComparison( IncrementalPatch patch1, IncrementalPatch patch2 )
		{
			int comparison = patch1.FromVersion.CompareTo( patch2.FromVersion );
			if( comparison != 0 )
				return comparison;

			// If FromVersion's are the same, the patch with higher ToVersion should be put in front of the other
			return patch2.ToVersion.CompareTo( patch1.ToVersion );
		}
		#endregion

		#region IO Functions
		public static bool CheckWriteAccessToFolder( string path )
		{
			path = GetPathWithTrailingSeparatorChar( path );
			string testFilePath = path + "_test__.sptest";

			try
			{
				if( !Directory.Exists( path ) )
					Directory.CreateDirectory( path );

				File.Create( testFilePath ).Close();

				// Check if file is created inside VirtualStore directory (UAC-issue on Windows)
				string root = Path.GetPathRoot( testFilePath );
				string virtualPathRelative = Path.Combine( "VirtualStore", testFilePath.Substring( root.Length ) );
				string virtualPathAbsolute = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), virtualPathRelative );
				if( File.Exists( virtualPathAbsolute ) )
					return false;

				virtualPathAbsolute = Path.Combine( root, virtualPathRelative );
				if( File.Exists( virtualPathAbsolute ) )
					return false;
			}
			catch( UnauthorizedAccessException )
			{
				return false;
			}
			catch( SecurityException )
			{
				return false;
			}
			finally
			{
				try
				{
					if( File.Exists( testFilePath ) )
						File.Delete( testFilePath );
				}
				catch { }
			}

			return true;
		}

		public static void CopyFile( string from, string to )
		{
			// Replacing a file that is in use can throw IOException; in such cases,
			// waiting for a short time might resolve the issue
			for( int i = 8; i >= 0; i-- )
			{
				if( i > 0 )
				{
					try
					{
						File.Copy( from, to, true );
						break;
					}
					catch( IOException )
					{
						Thread.Sleep( 500 );
					}
				}
				else
					File.Copy( from, to, true );
			}
		}

		public static void MoveFile( string fromAbsolutePath, string toAbsolutePath )
		{
			if( File.Exists( toAbsolutePath ) )
			{
				CopyFile( fromAbsolutePath, toAbsolutePath );
				File.Delete( fromAbsolutePath );
			}
			else
			{
				Directory.CreateDirectory( Path.GetDirectoryName( toAbsolutePath ) );
				File.Move( fromAbsolutePath, toAbsolutePath );
			}
		}

		public static void MoveDirectory( string fromAbsolutePath, string toAbsolutePath )
		{
			bool haveSameRoot = Directory.GetDirectoryRoot( fromAbsolutePath ).Equals( Directory.GetDirectoryRoot( toAbsolutePath ), StringComparison.OrdinalIgnoreCase );

			// Moving a directory between two roots/drives via Directory.Move throws an IOException
			if( haveSameRoot && !Directory.Exists( toAbsolutePath ) )
			{
				Directory.CreateDirectory( new DirectoryInfo( toAbsolutePath ).Parent.FullName );
				Directory.Move( fromAbsolutePath, toAbsolutePath );
			}
			else
			{
				Directory.CreateDirectory( toAbsolutePath );
				MoveDirectoryMerge( new DirectoryInfo( fromAbsolutePath ), GetPathWithTrailingSeparatorChar( toAbsolutePath ), haveSameRoot );
				DeleteDirectory( fromAbsolutePath );
			}
		}

		private static void MoveDirectoryMerge( DirectoryInfo fromDir, string toAbsolutePath, bool haveSameRoot )
		{
			FileInfo[] files = fromDir.GetFiles();
			for( int i = 0; i < files.Length; i++ )
			{
				FileInfo fileInfo = files[i];
				CopyFile( fileInfo.FullName, toAbsolutePath + fileInfo.Name );
			}

			DirectoryInfo[] subDirectories = fromDir.GetDirectories();
			for( int i = 0; i < subDirectories.Length; i++ )
			{
				DirectoryInfo directoryInfo = subDirectories[i];
				string directoryAbsolutePath = toAbsolutePath + directoryInfo.Name + Path.DirectorySeparatorChar;
				if( haveSameRoot && !Directory.Exists( directoryAbsolutePath ) )
					directoryInfo.MoveTo( directoryAbsolutePath );
				else
				{
					Directory.CreateDirectory( directoryAbsolutePath );
					MoveDirectoryMerge( directoryInfo, directoryAbsolutePath, haveSameRoot );
				}
			}
		}

		public static void CopyDirectory( string fromAbsolutePath, string toAbsolutePath )
		{
			if( !Directory.Exists( toAbsolutePath ) )
				Directory.CreateDirectory( toAbsolutePath );

			CopyDirectoryInternal( new DirectoryInfo( fromAbsolutePath ), GetPathWithTrailingSeparatorChar( toAbsolutePath ) );
		}

		private static void CopyDirectoryInternal( DirectoryInfo fromDir, string toAbsolutePath )
		{
			FileInfo[] files = fromDir.GetFiles();
			for( int i = 0; i < files.Length; i++ )
			{
				FileInfo fileInfo = files[i];
				CopyFile( fileInfo.FullName, toAbsolutePath + fileInfo.Name );
			}

			DirectoryInfo[] subDirectories = fromDir.GetDirectories();
			for( int i = 0; i < subDirectories.Length; i++ )
			{
				string directoryAbsolutePath = toAbsolutePath + subDirectories[i].Name + Path.DirectorySeparatorChar;
				Directory.CreateDirectory( directoryAbsolutePath );
				CopyDirectoryInternal( subDirectories[i], directoryAbsolutePath );
			}
		}

		public static void DeleteDirectory( string path )
		{
			if( Directory.Exists( path ) )
			{
				// Deleting a directory immediately after deleting a file inside it can sometimes
				// throw IOException; in such cases, waiting for a short time should resolve the issue
				for( int i = 4; i >= 0; i-- )
				{
					if( i > 0 )
					{
						try
						{
							Directory.Delete( path, true );
							break;
						}
						catch( IOException )
						{
							Thread.Sleep( 500 );
						}
					}
					else
						Directory.Delete( path, true );
				}

				while( Directory.Exists( path ) )
					Thread.Sleep( 100 );
			}
		}

		public static VersionInfo GetVersionInfoFromPath( string path )
		{
			string xmlContent = File.ReadAllText( path );
			return DeserializeXMLToVersionInfo( xmlContent );
		}

		public static IncrementalPatchInfo GetIncrementalPatchInfoFromPath( string path )
		{
			string xmlContent = File.ReadAllText( path );
			return DeserializeXMLToIncrementalPatchInfo( xmlContent );
		}

		public static ProjectInfo GetProjectInfoFromPath( string path )
		{
			string xmlContent = File.ReadAllText( path );
			return DeserializeXMLToProjectInfo( xmlContent );
		}

		public static VersionCode GetVersion( string rootPath, string projectName )
		{
			rootPath = GetPathWithTrailingSeparatorChar( rootPath ) + projectName + PatchParameters.VERSION_HOLDER_FILENAME_POSTFIX;
			return File.Exists( rootPath ) ? File.ReadAllText( rootPath ) : null;
		}

		public static void SetVersion( string rootPath, string projectName, VersionCode version )
		{
			Directory.CreateDirectory( rootPath );
			File.WriteAllText( GetPathWithTrailingSeparatorChar( rootPath ) + projectName + PatchParameters.VERSION_HOLDER_FILENAME_POSTFIX, version );
		}
		#endregion

		#region Other Functions
		public static string GetPathWithTrailingSeparatorChar( string path )
		{
			char trailingChar = path[path.Length - 1];
			if( trailingChar != Path.DirectorySeparatorChar && trailingChar != Path.AltDirectorySeparatorChar )
				path += Path.DirectorySeparatorChar;

			return path;
		}

		public static string GetCurrentExecutablePath()
		{
			using( Process process = Process.GetCurrentProcess() )
			{
				using( ProcessModule mainModule = process.MainModule )
				{
					return Path.GetFullPath( mainModule.FileName );
				}
			}
		}

		public static string GetDefaultSelfPatcherExecutablePath( string selfPatcherExecutableName = "SelfPatcher.exe" )
		{
			string selfPatcherDirectory = Path.Combine( Path.GetDirectoryName( GetCurrentExecutablePath() ), PatchParameters.SELF_PATCHER_DIRECTORY );
			return Path.Combine( selfPatcherDirectory, selfPatcherExecutableName );
		}

		public static string GetCurrentAppVersion( string projectName = null )
		{
			string searchPattern = string.Concat( "*", projectName == null ? "" : projectName.Trim(), PatchParameters.VERSION_HOLDER_FILENAME_POSTFIX );
			string[] versionHolders = Directory.GetFiles( Path.GetDirectoryName( GetCurrentExecutablePath() ), searchPattern, SearchOption.AllDirectories );
			if( versionHolders.Length > 0 )
				return File.ReadAllText( versionHolders[0] );

			return null;
		}

		public static int GetNumberOfRunningProcesses( string executablePath )
		{
			if( !File.Exists( executablePath ) )
				return 0;

			int result = 0;
			try
			{
				string executableFullPath = Path.GetFullPath( executablePath );
				Process[] processes = Process.GetProcessesByName( Path.GetFileNameWithoutExtension( executablePath ) );
				for( int i = 0; i < processes.Length; i++ )
				{
					try
					{
						using( Process process = processes[i] )
						{
							using( ProcessModule mainModule = process.MainModule )
							{
								if( executableFullPath.Equals( Path.GetFullPath( mainModule.FileName ), StringComparison.OrdinalIgnoreCase ) )
									result++;
							}
						}
					}
					catch { }
				}
			}
			catch { }

			return result;
		}

		public static bool IsProjectNameValid( string projectName )
		{
			if( string.IsNullOrEmpty( projectName ) )
				return false;

			for( int i = 0; i < projectName.Length; i++ )
			{
				char ch = projectName[i];
				if( ( ch < '0' || ch > '9' ) && ( ch < 'a' || ch > 'z' ) && ( ch < 'A' || ch > 'Z' ) )
					return false;
			}

			return true;
		}

		public static bool TryParseIntSimple( string s, out int result )
		{
			result = 0;
			if( string.IsNullOrEmpty( s ) )
				return false;

			bool isNegative = s[0] == '-';
			for( int i = isNegative ? 1 : 0; i < s.Length; i++ )
			{
				char ch = s[i];
				if( ch < '0' || ch > '9' )
					return false;

				result = result * 10 + ( ch - '0' );
			}

			if( isNegative )
				result = -result;

			return true;
		}

		public static Regex WildcardToRegex( string wildcardStr )
		{
			return new Regex( "^" + Regex.Escape( wildcardStr ).Replace( "\\?", "." ).Replace( "\\*", ".*" ) + "$", RegexOptions.None );
		}

		internal static Thread CreateBackgroundThread( ThreadStart start )
		{
			return new Thread( start )
			{
				IsBackground = true,
				CurrentCulture = CultureInfo.InvariantCulture, // To receive English exception messages
				CurrentUICulture = CultureInfo.InvariantCulture // To receive English exception messages
			};
		}

		internal static Thread CreateBackgroundThread( ParameterizedThreadStart start )
		{
			return new Thread( start )
			{
				IsBackground = true,
				CurrentCulture = CultureInfo.InvariantCulture, // To receive English exception messages
				CurrentUICulture = CultureInfo.InvariantCulture // To receive English exception messages
			};
		}

		// Handles 3 kinds of links (they can be preceeded by https://):
		// - drive.google.com/open?id=FILEID
		// - drive.google.com/file/d/FILEID/view?usp=sharing
		// - drive.google.com/uc?id=FILEID&export=download
		public static string GetGoogleDriveDownloadLinkFromUrl( string url )
		{
			int index = url.IndexOf( "id=" );
			int closingIndex;
			if( index > 0 )
			{
				index += 3;
				closingIndex = url.IndexOf( '&', index );
				if( closingIndex < 0 )
					closingIndex = url.Length;
			}
			else
			{
				index = url.IndexOf( "file/d/" );
				if( index < 0 ) // url is not in any of the supported forms
					return string.Empty;

				index += 7;

				closingIndex = url.IndexOf( '/', index );
				if( closingIndex < 0 )
				{
					closingIndex = url.IndexOf( '?', index );
					if( closingIndex < 0 )
						closingIndex = url.Length;
				}
			}

			return string.Format( "https://drive.google.com/uc?id={0}&export=download", url.Substring( index, closingIndex - index ) );
		}
		#endregion
	}
}