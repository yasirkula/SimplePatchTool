using System.IO;
using Octodiff.Core;
using Octodiff.Diagnostics;

namespace SimplePatchToolCore
{
	public static class OctoUtils
	{
		public static void CalculateDelta( string sourcePath, string targetPath, string deltaPath, int quality = 3, FilePatchProgress progressReporter = null )
		{
			// Try different chunk sizes to find the smallest diff file
			if( quality < 1 )
				quality = 1;

			int[] chunkSizes = new int[quality * 2 - 1];
			chunkSizes[0] = SignatureBuilder.DefaultChunkSize;

			int validChunkSizes = 1;
			int currentChunkSize = chunkSizes[0];
			for( int i = 1; i < quality; i++ )
			{
				currentChunkSize /= 2;
				if( currentChunkSize < SignatureBuilder.MinimumChunkSize )
					break;

				chunkSizes[validChunkSizes++] = currentChunkSize;
			}

			currentChunkSize = chunkSizes[0];
			for( int i = 1; i < quality; i++ )
			{
				currentChunkSize *= 2;
				if( currentChunkSize > SignatureBuilder.MaximumChunkSize )
					break;

				chunkSizes[validChunkSizes++] = currentChunkSize;
			}

			string deltaPathTemp = deltaPath + ".detmp";
			string signaturePathTemp = deltaPath + ".sgtmp";
			long deltaSize = 0L;
			for( int i = 0; i < validChunkSizes; i++ )
			{
				if( i == 0 )
				{
					CalculateDeltaInternal( sourcePath, targetPath, deltaPath, signaturePathTemp, chunkSizes[i], progressReporter );
					deltaSize = new FileInfo( deltaPath ).Length;
				}
				else
				{
					CalculateDeltaInternal( sourcePath, targetPath, deltaPathTemp, signaturePathTemp, chunkSizes[i], progressReporter );

					long newDeltaSize = new FileInfo( deltaPathTemp ).Length;
					if( newDeltaSize < deltaSize )
					{
						PatchUtils.MoveFile( deltaPathTemp, deltaPath );
						deltaSize = newDeltaSize;
					}
				}
			}

			File.Delete( deltaPathTemp );
			File.Delete( signaturePathTemp );
		}

		private static void CalculateDeltaInternal( string sourcePath, string targetPath, string deltaPath, string signaturePath, int chunkSize, FilePatchProgress progressReporter = null )
		{
			using( var signatureStream = new FileStream( signaturePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read ) )
			{
				using( var basisStream = new FileStream( sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read ) )
				{
					SignatureBuilder sb = new SignatureBuilder { ChunkSize = (short) chunkSize };
					sb.Build( basisStream, new SignatureWriter( signatureStream ) );
				}

				signatureStream.Position = 0L;

				using( var newFileStream = new FileStream( targetPath, FileMode.Open, FileAccess.Read, FileShare.Read ) )
				using( var deltaStream = new FileStream( deltaPath, FileMode.Create, FileAccess.Write, FileShare.Read ) )
				{
					IProgressReporter reporter = progressReporter;
					if( reporter == null )
						reporter = new NullProgressReporter();

					new DeltaBuilder().BuildDelta( newFileStream, new SignatureReader( signatureStream, reporter ), new AggregateCopyOperationsDecorator( new BinaryDeltaWriter( deltaStream ) ) );
				}
			}
		}

		public static void ApplyDelta( string sourcePath, string targetPath, string deltaPath, FilePatchProgress progressReporter = null )
		{
			using( var basisStream = new FileStream( sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read ) )
			using( var deltaStream = new FileStream( deltaPath, FileMode.Open, FileAccess.Read, FileShare.Read ) )
			using( var newFileStream = new FileStream( targetPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read ) )
			{
				IProgressReporter reporter = progressReporter;
				if( reporter == null )
					reporter = new NullProgressReporter();

				new DeltaApplier().Apply( basisStream, new BinaryDeltaReader( deltaStream, reporter ), newFileStream );
			}
		}
	}
}

namespace Octodiff.Core
{
	internal static class StructuralComparisons
	{
		internal static class StructuralEqualityComparer
		{
			internal static bool Equals( byte[] arr1, byte[] arr2 )
			{
				if( arr1 != null )
				{
					if( arr2 == null )
						return false;

					if( ReferenceEquals( arr1, arr2 ) )
						return true;

					if( arr1.Length != arr2.Length )
						return false;

					for( int i = 0; i < arr1.Length; i++ )
					{
						if( arr1[i] != arr2[i] )
							return false;
					}

					return true;
				}

				return arr2 == null;
			}
		}
	}
}