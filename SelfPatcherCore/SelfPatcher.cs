using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SelfPatcherCore
{
	public class SelfPatcher
	{
		private enum Op { Delete, Move };

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

				listener.OnLogAppeared( string.Concat( "Updating from v", instructions.Substring( 0, tokenEnd ), ", please don't close this window!" ) );

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

				listener.OnLogAppeared( "Successful..!" );
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
					listener.OnLogAppeared( "ERROR: post update executable does not exist" );
				else
				{
					FileInfo executable = new FileInfo( postSelfPatcher );
					Process.Start( new ProcessStartInfo( executable.Name ) { WorkingDirectory = executable.DirectoryName } );
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
			{
				if( Directory.Exists( to ) )
					MergeDirectories( from, to );
				else
				{
					Directory.CreateDirectory( Directory.GetParent( to ).FullName );
					Directory.Move( from, to );
				}
			}
		}
		#endregion

		#region Utilities
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
					listener.OnLogAppeared( e.Message + ", retrying in 0.5 seconds: " + to );
					Thread.Sleep( 500 );
				}
			}
		}

		private void MergeDirectories( string fromAbsolutePath, string toAbsolutePath )
		{
			toAbsolutePath = GetPathWithTrailingSeparatorChar( toAbsolutePath );

			MergeDirectories( new DirectoryInfo( fromAbsolutePath ), new DirectoryInfo( toAbsolutePath ), toAbsolutePath );
			DeleteDirectory( fromAbsolutePath );
		}

		private void MergeDirectories( DirectoryInfo from, DirectoryInfo to, string targetAbsolutePath )
		{
			FileInfo[] files = from.GetFiles();
			for( int i = 0; i < files.Length; i++ )
				CopyFile( files[i].FullName, targetAbsolutePath + files[i].Name );

			DirectoryInfo[] subDirectories = from.GetDirectories();
			for( int i = 0; i < subDirectories.Length; i++ )
			{
				DirectoryInfo directoryInfo = subDirectories[i];
				string directoryAbsolutePath = targetAbsolutePath + directoryInfo.Name + Path.DirectorySeparatorChar;
				if( Directory.Exists( directoryAbsolutePath ) )
					MergeDirectories( directoryInfo, new DirectoryInfo( directoryAbsolutePath ), directoryAbsolutePath );
				else
					directoryInfo.MoveTo( directoryAbsolutePath );
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