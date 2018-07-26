using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SimplePatchToolSelfPatcher
{
	public class Program
	{
		private const string OP_SEPARATOR = "><";
		private const string DELETE_OP = "_#DELETE#_";
		private const string MOVE_OP = "_#MOVE#_";

		private enum Op { Delete, Move };

		public static void Main( string[] args )
		{
			try
			{
				if( args.Length < 2 || args.Length > 3 )
				{
					ShowMessage( "ERROR: args format should be {instructions} {completed instructions} {post update executable (optional)}", true );
					return;
				}

				string instructionsPath = args[0];
				string completedInstructionsPath = args[1];

				if( !File.Exists( instructionsPath ) )
				{
					ShowMessage( "ERROR: instructions file \"" + instructionsPath + "\" does not exist", true );
					return;
				}

				string instructions = File.ReadAllText( instructionsPath );
				if( string.IsNullOrEmpty( instructions ) )
				{
					ShowMessage( "ERROR: missing instructions", true );
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
					ShowMessage( "ERROR: invalid instructions file", true );
					return;
				}

				ShowMessage( string.Concat( "Updating from v", instructions.Substring( 0, tokenEnd ), ", please don't close this window!" ) );

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

							if( File.Exists( token ) )
								File.Delete( token );
							else if( Directory.Exists( token ) )
								Directory.Delete( token, true );
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

				if( args.Length == 3 )
				{
					if( !File.Exists( args[2] ) )
					{
						ShowMessage( "ERROR: post update executable does not exist" );
						return;
					}

					FileInfo executable = new FileInfo( args[2] );
					Process.Start( new ProcessStartInfo( executable.Name ) { WorkingDirectory = executable.DirectoryName } );
				}

				ShowMessage( "Successful..!" );
			}
			catch( Exception e )
			{
				ShowMessage( "ERROR: " + e.ToString(), true );
				return;
			}
		}

		private static void MoveFiles( string from, string to )
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

		#region Utilities
		private static void CopyFile( string from, string to )
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
					ShowMessage( e.Message + ", retrying in 0.5 seconds: " + to );
					Thread.Sleep( 500 );
				}
			}
		}

		private static void MergeDirectories( string fromAbsolutePath, string toAbsolutePath )
		{
			toAbsolutePath = GetPathWithTrailingSeparatorChar( toAbsolutePath );

			MergeDirectories( new DirectoryInfo( fromAbsolutePath ), new DirectoryInfo( toAbsolutePath ), toAbsolutePath );
			Directory.Delete( fromAbsolutePath, true );
		}

		private static void MergeDirectories( DirectoryInfo from, DirectoryInfo to, string targetAbsolutePath )
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

		private static string GetPathWithTrailingSeparatorChar( string path )
		{
			char trailingChar = path[path.Length - 1];
			if( trailingChar != Path.DirectorySeparatorChar && trailingChar != Path.AltDirectorySeparatorChar )
				path += Path.DirectorySeparatorChar;

			return path;
		}

		private static void ShowMessage( string msg, bool waitForUserInput = false )
		{
			Console.WriteLine( msg );

			if( waitForUserInput )
				Console.ReadKey( true );
		}
		#endregion
	}
}