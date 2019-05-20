using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SelfPatcherCore
{
	public class SelfPatcher
	{
		private enum Op { Delete, Move };

		private const int WARMUP_TIME = 2000;

		private const string OP_SEPARATOR = "><";
		private const string DELETE_OP = "_#DELETE#_";
		private const string MOVE_OP = "_#MOVE#_";

		private readonly ISelfPatcherListener listener;
		private string postSelfPatcher;

		/// <exception cref = "ArgumentNullException">A listener is not provided</exception>
		public SelfPatcher( ISelfPatcherListener listener )
		{
			if( listener == null )
				throw new ArgumentNullException( "listener" );

			this.listener = listener;
		}

		public void Run( string[] args )
		{
			try
			{
				if( args.Length < 2 || args.Length > 3 )
				{
					listener.OnFail( "ERROR: args format should be {instructions} {completed instructions} {post update executable (optional)}" );
					return;
				}

				if( args.Length >= 3 )
					postSelfPatcher = args[2];

				string instructionsPath = args[0];
				string completedInstructionsPath = args[1];

				if( !File.Exists( instructionsPath ) )
				{
					listener.OnFail( "ERROR: instructions file \"" + instructionsPath + "\" does not exist" );
					return;
				}

				string instructions = File.ReadAllText( instructionsPath );
				if( string.IsNullOrEmpty( instructions ) )
				{
					listener.OnFail( "ERROR: missing instructions" );
					return;
				}

				int completedInstructions = 0;
				if( File.Exists( completedInstructionsPath ) && !int.TryParse( File.ReadAllText( completedInstructionsPath ), out completedInstructions ) )
					completedInstructions = 0;

				Op currentOp = Op.Delete;
				string moveFrom = null;
				int currentInstruction = 0;
				int tokenStart = 0, tokenEnd = instructions.IndexOf( OP_SEPARATOR );
				if( tokenEnd < 0 )
				{
					listener.OnFail( "ERROR: invalid instructions file" );
					return;
				}

				Log( string.Concat( "Updating from v", instructions.Substring( 0, tokenEnd ), ", please don't close this window!" ) );

				// Wait for a while before starting the self patch (some files might still be blocked/in use/not released
				// for a couple of milliseconds immediately after the self patcher is started)
				Thread.Sleep( WARMUP_TIME );

				int numberOfInstructions = 0;
				while( tokenStart < instructions.Length )
				{
					tokenStart = tokenEnd + OP_SEPARATOR.Length;
					if( tokenStart >= instructions.Length )
						break;

					tokenEnd = instructions.IndexOf( OP_SEPARATOR, tokenStart );
					if( tokenEnd < 0 )
						break;

					string token = instructions.Substring( tokenStart, tokenEnd - tokenStart );
					if( token.Length == 0 )
						continue;

					if( token != DELETE_OP && token != MOVE_OP )
						numberOfInstructions++;
				}

				if( numberOfInstructions > 0 )
				{
					tokenStart = 0;
					tokenEnd = instructions.IndexOf( OP_SEPARATOR );
					while( tokenStart < instructions.Length )
					{
						try
						{
							listener.OnProgressChanged( currentInstruction, numberOfInstructions );
						}
						catch { }

						tokenStart = tokenEnd + OP_SEPARATOR.Length;
						if( tokenStart >= instructions.Length )
							break;

						tokenEnd = instructions.IndexOf( OP_SEPARATOR, tokenStart );
						if( tokenEnd < 0 )
							break;

						string token = instructions.Substring( tokenStart, tokenEnd - tokenStart );
						if( token.Length == 0 )
							continue;

						if( token == DELETE_OP )
							currentOp = Op.Delete;
						else if( token == MOVE_OP )
						{
							currentOp = Op.Move;
							moveFrom = null;
						}
						else
						{
							currentInstruction++;

							if( currentOp == Op.Delete )
							{
								if( currentInstruction <= completedInstructions )
									continue;

								File.WriteAllText( completedInstructionsPath, ( currentInstruction - 1 ).ToString() );
								Delete( token );
							}
							else if( currentOp == Op.Move )
							{
								if( moveFrom == null )
									moveFrom = token;
								else
								{
									if( currentInstruction <= completedInstructions )
										continue;

									File.WriteAllText( completedInstructionsPath, ( currentInstruction - 1 ).ToString() );

									MoveFiles( moveFrom, token );
									moveFrom = null;
								}
							}
						}
					}
				}

				Log( "Successful..!" );
				listener.OnSuccess();
			}
			catch( Exception e )
			{
				listener.OnFail( "ERROR: " + e.ToString() );
			}
		}

		public void ExecutePostSelfPatcher()
		{
			if( !string.IsNullOrEmpty( postSelfPatcher ) )
			{
				if( !File.Exists( postSelfPatcher ) )
					Log( "ERROR: post update executable does not exist" );
				else
				{
					FileInfo executable = new FileInfo( postSelfPatcher );
					Process.Start( new ProcessStartInfo( executable.FullName ) { WorkingDirectory = executable.DirectoryName } );
				}
			}

			Process.GetCurrentProcess().Kill();
		}

		#region Operations
		private void Delete( string path )
		{
			if( File.Exists( path ) )
				File.Delete( path );
			else
				DeleteDirectory( path );
		}

		private void MoveFiles( string from, string to )
		{
			if( File.Exists( from ) )
			{
				if( File.Exists( to ) )
				{
					CopyFile( from, to );
					File.Delete( from );
				}
				else
				{
					Directory.CreateDirectory( Path.GetDirectoryName( to ) );
					File.Move( from, to );
				}
			}
			else if( Directory.Exists( from ) )
				MoveDirectory( from, to );
		}
		#endregion

		#region Utilities
		private void Log( string log )
		{
			try
			{
				listener.OnLogAppeared( log );
			}
			catch { }
		}

		private void CopyFile( string from, string to )
		{
			while( true )
			{
				try
				{
					File.Copy( from, to, true );
					break;
				}
				catch( IOException e )
				{
					// Keep checking the status of the file with 0.5s interval until it is released
					Log( e.Message + ", retrying in 0.5 seconds: " + to );
					Thread.Sleep( 500 );
				}
			}
		}

		private void MoveDirectory( string fromAbsolutePath, string toAbsolutePath )
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

		private void MoveDirectoryMerge( DirectoryInfo fromDir, string toAbsolutePath, bool haveSameRoot )
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

		private void DeleteDirectory( string path )
		{
			if( Directory.Exists( path ) )
			{
				// Deleting a directory immediately after deleting a file inside it can sometimes
				// throw IOException; in such cases, waiting for a short time should resolve the issue
				for( int i = 8; i >= 0; i-- )
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

		private string GetPathWithTrailingSeparatorChar( string path )
		{
			char trailingChar = path[path.Length - 1];
			if( trailingChar != Path.DirectorySeparatorChar && trailingChar != Path.AltDirectorySeparatorChar )
				path += Path.DirectorySeparatorChar;

			return path;
		}
		#endregion
	}
}