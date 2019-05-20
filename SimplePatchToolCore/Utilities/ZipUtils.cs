using ICSharpCode.SharpZipLib.Tar;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Compression = SevenZip.Compression;

namespace SimplePatchToolCore
{
	public enum CompressionFormat { LZMA = 0, GZIP = 1 };

	public static class ZipUtils
	{
		public static void CompressFile( string inFile, string outFile, CompressionFormat format )
		{
			using( FileStream input = new FileStream( inFile, FileMode.Open, FileAccess.Read ) )
			using( FileStream output = new FileStream( outFile, FileMode.Create ) )
			{
				if( format == CompressionFormat.LZMA )
				{
					// Credit: http://stackoverflow.com/questions/7646328/how-to-use-the-7z-sdk-to-compress-and-decompress-a-file
					Compression.LZMA.Encoder coder = new Compression.LZMA.Encoder();

					// Write the encoder properties
					coder.WriteCoderProperties( output );

					// Write the decompressed file size.
					output.Write( BitConverter.GetBytes( input.Length ), 0, 8 );

					// Encode the file.
					coder.Code( input, output, input.Length, -1, null );
				}
				else
				{
					using( GZipStream compressionStream = new GZipStream( output, CompressionMode.Compress ) )
					{
						input.CopyTo( compressionStream );
					}
				}
			}
		}

		public static void DecompressFile( string inFile, string outFile, CompressionFormat format )
		{
			using( FileStream input = new FileStream( inFile, FileMode.Open, FileAccess.Read ) )
			using( FileStream output = new FileStream( outFile, FileMode.Create ) )
			{
				if( format == CompressionFormat.LZMA )
				{
					// Credit: http://stackoverflow.com/questions/7646328/how-to-use-the-7z-sdk-to-compress-and-decompress-a-file
					Compression.LZMA.Decoder coder = new Compression.LZMA.Decoder();

					// Read the decoder properties
					byte[] properties = new byte[5];
					input.Read( properties, 0, 5 );

					// Read in the decompress file size.
					byte[] fileLengthBytes = new byte[8];
					input.Read( fileLengthBytes, 0, 8 );
					long fileLength = BitConverter.ToInt64( fileLengthBytes, 0 );

					coder.SetDecoderProperties( properties );
					coder.Code( input, output, input.Length, fileLength, null );
				}
				else
				{
					using( GZipStream decompressionStream = new GZipStream( input, CompressionMode.Decompress ) )
					{
						decompressionStream.CopyTo( output );
					}
				}
			}
		}

		public static void CompressFolder( string inFolder, string outFile, CompressionFormat format )
		{
			CompressFolder( inFolder, outFile, format, new List<Regex>( 0 ) );
		}

		internal static void CompressFolder( string inFolder, string outFile, CompressionFormat format, List<Regex> ignoredPathsRegex )
		{
			string tarFilePath = outFile + "tmptar";

			// Source: https://github.com/icsharpcode/SharpZipLib/wiki/GZip-and-Tar-Samples#-create-a-tgz-targz
			using( FileStream outputStream = File.Create( tarFilePath ) )
			using( TarArchive tarArchive = TarArchive.CreateOutputTarArchive( outputStream ) )
			{
				// Currently, SharpZipLib only supports '/', and the folder path must not end with it
				if( inFolder[inFolder.Length - 1] == '\\' || inFolder[inFolder.Length - 1] == '/' )
					inFolder = inFolder.Substring( 0, inFolder.Length - 1 ).Replace( '\\', '/' );
				else
					inFolder = inFolder.Replace( '\\', '/' );

				tarArchive.RootPath = inFolder;
				CreateTarRecursive( tarArchive, new DirectoryInfo( inFolder ), "", ignoredPathsRegex );
			}

			CompressFile( tarFilePath, outFile, format );
			File.Delete( tarFilePath );
		}

		public static void DecompressFolder( string inFile, string outFolder, CompressionFormat format )
		{
			string tarFilePath = outFolder + "tmptar.tar";
			DecompressFile( inFile, tarFilePath, format );

			// Source: https://github.com/icsharpcode/SharpZipLib/wiki/GZip-and-Tar-Samples#--simple-full-extract-from-a-tar-archive
			using( Stream inStream = File.OpenRead( tarFilePath ) )
			using( TarArchive tarArchive = TarArchive.CreateInputTarArchive( inStream ) )
			{
				tarArchive.ExtractContents( outFolder );
			}

			File.Delete( tarFilePath );
		}

		// Source: https://github.com/icsharpcode/SharpZipLib/wiki/GZip-and-Tar-Samples#-create-a-tgz-targz
		private static void CreateTarRecursive( TarArchive tarArchive, DirectoryInfo directory, string relativePath, List<Regex> ignoredPathsRegex )
		{
			TarEntry tarEntry = TarEntry.CreateEntryFromFile( directory.FullName );
			tarArchive.WriteEntry( tarEntry, false );

			FileInfo[] files = directory.GetFiles();
			for( int i = 0; i < files.Length; i++ )
			{
				string fileRelativePath = relativePath + files[i].Name;
				if( !ignoredPathsRegex.PathMatchesPattern( fileRelativePath ) )
				{
					tarEntry = TarEntry.CreateEntryFromFile( files[i].FullName );
					tarArchive.WriteEntry( tarEntry, true );
				}
			}

			DirectoryInfo[] subDirectories = directory.GetDirectories();
			for( int i = 0; i < subDirectories.Length; i++ )
			{
				string directoryRelativePath = relativePath + subDirectories[i].Name + Path.DirectorySeparatorChar;
				if( !ignoredPathsRegex.PathMatchesPattern( directoryRelativePath ) )
					CreateTarRecursive( tarArchive, subDirectories[i], directoryRelativePath, ignoredPathsRegex );
			}
		}

		// Credit: https://stackoverflow.com/a/5730893/2373034
		private static void CopyTo( this Stream input, Stream output )
		{
			byte[] buffer = new byte[8 * 1024];
			int bytesRead;

			while( ( bytesRead = input.Read( buffer, 0, buffer.Length ) ) > 0 )
			{
				output.Write( buffer, 0, bytesRead );
			}
		}
	}
}