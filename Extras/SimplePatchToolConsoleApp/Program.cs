using SimplePatchToolCore;
using SimplePatchToolSecurity;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace SimplePatchToolConsoleApp
{
	public class Program
	{
		private class ConsoleCommand
		{
			public string name;
			public string[] requiredArgs;
			public string[] requiredArgsDescriptions;
			public string[] optionalArgs;
			public string[] optionalArgsDescriptions;
			public bool hidden;
			public Action function;

			public override string ToString()
			{
				StringBuilder sb = new StringBuilder( 500 );
				sb.Append( "===== " ).Append( EXECUTABLE_NAME ).Append( " " ).Append( name ).Append( " =====" ).AppendLine();

				ToStringArguments( sb, "Required arguments: ", requiredArgs, requiredArgsDescriptions );
				ToStringArguments( sb, "Optional arguments: ", optionalArgs, optionalArgsDescriptions );

				return sb.ToString();
			}

			private void ToStringArguments( StringBuilder sb, string label, string[] args, string[] argsDescriptions )
			{
				if( args.Length > 0 )
				{
					sb.AppendLine( label );

					for( int i = 0; i < args.Length; i++ )
					{
						sb.Append( "-" ).Append( args[i] );
						if( !string.IsNullOrEmpty( argsDescriptions[i] ) )
							sb.Append( "={" ).Append( argsDescriptions[i] ).Append( "} " );
						else
							sb.Append( " " );

						sb.AppendLine();
					}

					sb.AppendLine();
				}
			}
		}

		private const string EXECUTABLE_NAME = "Patcher";

		private static string[] args;
		private static readonly ConsoleCommand[] commands = new ConsoleCommand[]
		{
			new ConsoleCommand()
			{
				name = "project_create",
				requiredArgs = new string[] { "projectRoot" },
				requiredArgsDescriptions = new string[] { "Path of the project directory" },
				optionalArgs = new string[] { "silent" },
				optionalArgsDescriptions = new string[] { "" },
				hidden = false,
				function = CreateProject
			},
			new ConsoleCommand()
			{
				name = "project_generate_patch",
				requiredArgs = new string[] { "projectRoot" },
				requiredArgsDescriptions = new string[] { "Path of the project directory" },
				optionalArgs = new string[] { "silent" },
				optionalArgsDescriptions = new string[] { "" },
				hidden = false,
				function = ProjectGeneratePatch
			},
			new ConsoleCommand()
			{
				name = "project_update_links",
				requiredArgs = new string[] { "projectRoot" },
				requiredArgsDescriptions = new string[] { "Path of the project directory" },
				optionalArgs = new string[] { "silent" },
				optionalArgsDescriptions = new string[] { "" },
				hidden = false,
				function = ProjectUpdateLinks
			},
			new ConsoleCommand()
			{
				name = "project_sign_xmls",
				requiredArgs = new string[] { "projectRoot" },
				requiredArgsDescriptions = new string[] { "Path of the project directory" },
				optionalArgs = new string[] { "ignoreVersionInfo", "ignorePatchInfos", "silent" },
				optionalArgsDescriptions = new string[] { "", "", "" },
				hidden = false,
				function = ProjectSignXMLs
			},
			new ConsoleCommand()
			{
				name = "project_verify_xmls",
				requiredArgs = new string[] { "projectRoot" },
				requiredArgsDescriptions = new string[] { "Path of the project directory" },
				optionalArgs = new string[] { "ignoreVersionInfo", "ignorePatchInfos", "silent" },
				optionalArgsDescriptions = new string[] { "", "", "" },
				hidden = false,
				function = ProjectVerifyXMLs
			},
			new ConsoleCommand()
			{
				name = "create",
				requiredArgs = new string[] { "root", "out", "name", "version" },
				requiredArgsDescriptions = new string[] { "Root path", "Output path", "Project name", "Version" },
				optionalArgs = new string[] { "prevRoot", "prevPatchRoot", "ignoredPaths", "compressionRepair", "compressionInstaller", "compressionIncremental", "skipUnchangedPatchFiles", "dontCreateRepairPatch", "dontCreateInstallerPatch", "silent" },
				optionalArgsDescriptions = new string[] { "Previous version path", "Path of previous version's patch files", "Path of ignored paths list", "Repair patch's compression format (LZMA/GZIP/NONE)",
					"Installer patch's compression format (LZMA/GZIP/NONE)", "Incremental patch's compression format (LZMA/GZIP/NONE)", "", "", "", "" },
				hidden = true,
				function = CreatePatch
			},
			new ConsoleCommand()
			{
				name = "check_updates",
				requiredArgs = new string[] { "root", "versionURL" },
				requiredArgsDescriptions = new string[] { "Root path", "VersionInfo URL" },
				optionalArgs = new string[] { "checkVersionOnly", "silent", "versionInfoKey" },
				optionalArgsDescriptions = new string[] { "", "", "Path of VersionInfo verifier RSA key" },
				hidden = false,
				function = CheckForUpdates
			},
			new ConsoleCommand()
			{
				name = "apply",
				requiredArgs = new string[] { "root", "versionURL" },
				requiredArgsDescriptions = new string[] { "Root path", "VersionInfo URL" },
				optionalArgs = new string[] { "dontUseIncrementalPatch", "dontUseRepairPatch", "dontUseInstallerPatch", "verifyFiles", "silent", "versionInfoKey", "patchInfoKey" },
				optionalArgsDescriptions = new string[] { "", "", "", "", "", "Path of VersionInfo verifier RSA key", "Path of PatchInfo verifier RSA key" },
				hidden = false,
				function = ApplyPatch
			},
			new ConsoleCommand()
			{
				name = "update_links",
				requiredArgs = new string[] { "versionInfoPath", "linksPath" },
				requiredArgsDescriptions = new string[] { "VersionInfo path", "Download links path" },
				optionalArgs = new string[] { "silent" },
				optionalArgsDescriptions = new string[] { "" },
				hidden = true,
				function = UpdateLinks
			},
			new ConsoleCommand()
			{
				name = "sign_xml",
				requiredArgs = new string[] { "xml", "key" },
				requiredArgsDescriptions = new string[] { "XML path", "Private RSA key path" },
				optionalArgs = new string[] { },
				optionalArgsDescriptions = new string[] { },
				hidden = true,
				function = SignXML
			},
			new ConsoleCommand()
			{
				name = "verify_xml",
				requiredArgs = new string[] { "xml", "key" },
				requiredArgsDescriptions = new string[] { "XML path", "Public RSA key path" },
				optionalArgs = new string[] { },
				optionalArgsDescriptions = new string[] { },
				hidden = true,
				function = VerifyXML
			},
			new ConsoleCommand()
			{
				name = "generate_rsa_key_pair",
				requiredArgs = new string[] { "private", "public" },
				requiredArgsDescriptions = new string[] { "Private RSA key path", "Public RSA key path" },
				optionalArgs = new string[] { },
				optionalArgsDescriptions = new string[] { },
				hidden = true,
				function = GenerateRSAKeyPair
			},
			new ConsoleCommand()
			{
				name = "help",
				requiredArgs = new string[] { },
				requiredArgsDescriptions = new string[] { },
				optionalArgs = new string[] { },
				optionalArgsDescriptions = new string[] { },
				hidden = true,
				function = Help
			},
		};

		public static void Main( string[] args )
		{
			Program.args = args;
			if( args.Length > 0 )
			{
				for( int i = 0; i < commands.Length; i++ )
				{
					if( commands[i].name == args[0] )
					{
						int j = 0;
						while( j < commands[i].requiredArgs.Length && HasArgument( commands[i].requiredArgs[j] ) )
							j++;

						if( j >= commands[i].requiredArgs.Length )
						{
							// All required arguments are provided, execute the function
							try
							{
								commands[i].function();
							}
							catch( Exception e )
							{
								Console.WriteLine( e );
							}
						}
						else
							Console.WriteLine( commands[i].ToString() );

						return;
					}
				}
			}

			Help();
		}

		private static void Help()
		{
			// Print available commands to the console
			StringBuilder sb = new StringBuilder( 100 );
			sb.Append( "Usage: " ).Append( EXECUTABLE_NAME ).Append( " {Command} {-Argument(s)}\n\nAvailable Commands:\n" );
			for( int i = 0; i < commands.Length; i++ )
			{
				if( commands[i].hidden )
					continue;

				sb.AppendLine( commands[i].name );
			}

			Console.WriteLine( sb.ToString() );
		}

		private static void CreateProject()
		{
			ProjectManager project = new ProjectManager( GetArgument( "projectRoot" ) ).SilentMode( HasArgument( "silent" ) );
			project.CreateProject();
			WaitForProjectManager( project );

			Console.WriteLine( "\nOperation successful..." );
		}

		private static void ProjectGeneratePatch()
		{
			ProjectManager project = new ProjectManager( GetArgument( "projectRoot" ) ).SilentMode( HasArgument( "silent" ) );
			if( project.GeneratePatch() )
			{
				WaitForProjectManager( project );

				if( project.Result == PatchResult.Failed )
					Console.WriteLine( "\nOperation failed..." );
				else
					Console.WriteLine( "\nOperation successful..." );
			}
			else
				Console.WriteLine( "\nOperation could not be started; maybe it is already executing?" );
		}

		private static void ProjectUpdateLinks()
		{
			ProjectManager project = new ProjectManager( GetArgument( "projectRoot" ) ).SilentMode( HasArgument( "silent" ) );
			project.UpdateDownloadLinks();

			WaitForProjectManager( project );
		}

		private static void ProjectSignXMLs()
		{
			ProjectManager project = new ProjectManager( GetArgument( "projectRoot" ) ).SilentMode( HasArgument( "silent" ) );
			SecurityUtils.SignXMLsWithKeysInDirectory( project.GetXMLFiles( !HasArgument( "ignoreVersionInfo" ), !HasArgument( "ignorePatchInfos" ) ), project.utilitiesPath );

			Console.WriteLine( "\nOperation successful..." );
		}

		private static void ProjectVerifyXMLs()
		{
			string[] invalidXmls;

			ProjectManager project = new ProjectManager( GetArgument( "projectRoot" ) ).SilentMode( HasArgument( "silent" ) );
			if( !SecurityUtils.VerifyXMLsWithKeysInDirectory( project.GetXMLFiles( !HasArgument( "ignoreVersionInfo" ), !HasArgument( "ignorePatchInfos" ) ), project.utilitiesPath, out invalidXmls ) )
			{
				Console.WriteLine( "\nThe following XMLs could not be verified:\n" );
				for( int i = 0; i < invalidXmls.Length; i++ )
					Console.WriteLine( invalidXmls[i] );
			}
			else
				Console.WriteLine( "\nAll XMLs are verified..." );
		}

		private static void CreatePatch()
		{
			string prevRoot = GetArgument( "prevRoot" );
			string compRepair = GetArgument( "compressionRepair" );
			string compInstaller = GetArgument( "compressionInstaller" );
			string compIncremental = GetArgument( "compressionIncremental" );

			CompressionFormat repairCompression, installerCompression, incrementalCompression;

			if( compRepair == "GZIP" )
				repairCompression = CompressionFormat.GZIP;
			else if( compRepair == "NONE" )
				repairCompression = CompressionFormat.NONE;
			else
				repairCompression = CompressionFormat.LZMA;

			if( compInstaller == "GZIP" )
				installerCompression = CompressionFormat.GZIP;
			else if( compInstaller == "NONE" )
				installerCompression = CompressionFormat.NONE;
			else
				installerCompression = CompressionFormat.LZMA;

			if( compIncremental == "GZIP" )
				incrementalCompression = CompressionFormat.GZIP;
			else if( compIncremental == "NONE" )
				incrementalCompression = CompressionFormat.NONE;
			else
				incrementalCompression = CompressionFormat.LZMA;

			PatchCreator patchCreator = new PatchCreator( GetArgument( "root" ), GetArgument( "out" ), GetArgument( "name" ), GetArgument( "version" ) ).
				LoadIgnoredPathsFromFile( GetArgument( "ignoredPaths" ) ).SetCompressionFormat( repairCompression, installerCompression, incrementalCompression ).
				CreateRepairPatch( !HasArgument( "dontCreateRepairPatch" ) ).CreateIncrementalPatch( prevRoot != null, prevRoot ).CreateInstallerPatch( !HasArgument( "dontCreateInstallerPatch" ) ).
				SetPreviousPatchFilesRoot( GetArgument( "prevPatchRoot" ), HasArgument( "skipUnchangedPatchFiles" ) ).SilentMode( HasArgument( "silent" ) );
			bool hasStarted = patchCreator.Run();
			if( hasStarted )
			{
				WaitForPatchCreator( patchCreator );

				if( patchCreator.Result == PatchResult.Failed )
					Console.WriteLine( "\nOperation failed..." );
				else
					Console.WriteLine( "\nOperation successful..." );
			}
			else
				Console.WriteLine( "\nOperation could not be started; maybe it is already executing?" );
		}

		private static void CheckForUpdates()
		{
			string versionInfoKeyPath = GetArgument( "versionInfoKey" );

			SimplePatchTool patcher = new SimplePatchTool( GetArgument( "root" ), GetArgument( "versionURL" ) ).SilentMode( HasArgument( "silent" ) );

			if( versionInfoKeyPath != null )
			{
				string publicKey = File.ReadAllText( versionInfoKeyPath );
				patcher.UseVersionInfoVerifier( ( ref string xml ) => XMLSigner.VerifyXMLContents( xml, publicKey ) );
			}

			bool hasStarted = patcher.CheckForUpdates( HasArgument( "checkVersionOnly" ) );
			if( hasStarted )
			{
				WaitForPatcher( patcher );

				if( patcher.Result == PatchResult.Failed )
					Console.WriteLine( "\nOperation failed: " + patcher.FailReason + " " + ( patcher.FailDetails ?? "" ) );
				else if( patcher.Result == PatchResult.AlreadyUpToDate )
					Console.WriteLine( "\nAlready up-to-date!" );
				else
					Console.WriteLine( "\nThere is an update!" );
			}
			else
				Console.WriteLine( "\nCould not check for updates; maybe an operation is already running?" );
		}

		private static void ApplyPatch()
		{
			bool silent = HasArgument( "silent" );
			string versionInfoKeyPath = GetArgument( "versionInfoKey" );
			string patchInfoKeyPath = GetArgument( "patchInfoKey" );

			SimplePatchTool patcher = new SimplePatchTool( GetArgument( "root" ), GetArgument( "versionURL" ) ).
				UseIncrementalPatch( !HasArgument( "dontUseIncrementalPatch" ) ).UseRepairPatch( !HasArgument( "dontUseRepairPatch" ) ).
				CheckForMultipleRunningInstances( false ).VerifyFilesOnServer( HasArgument( "verifyFiles" ) ).LogProgress( !silent ).SilentMode( silent );

			if( versionInfoKeyPath != null )
			{
				string publicKey = File.ReadAllText( versionInfoKeyPath );
				patcher.UseVersionInfoVerifier( ( ref string xml ) => XMLSigner.VerifyXMLContents( xml, publicKey ) );
			}

			if( patchInfoKeyPath != null )
			{
				string publicKey = File.ReadAllText( patchInfoKeyPath );
				patcher.UsePatchInfoVerifier( ( ref string xml ) => XMLSigner.VerifyXMLContents( xml, publicKey ) );
			}

			bool hasPatchStarted = patcher.Run( false );
			if( hasPatchStarted )
			{
				WaitForPatcher( patcher );

				if( patcher.Result == PatchResult.Failed )
					Console.WriteLine( "\nPatch failed: " + patcher.FailReason + " " + ( patcher.FailDetails ?? "" ) );
				else if( patcher.Result == PatchResult.AlreadyUpToDate )
					Console.WriteLine( "\nAlready up-to-date!" );
				else
					Console.WriteLine( "\nPatch is successful!" );
			}
			else
				Console.WriteLine( "\nPatch could not be started; maybe it is already executing?" );
		}

		private static void UpdateLinks()
		{
			PatchUpdater patchUpdater = !HasArgument( "silent" ) ? new PatchUpdater( GetArgument( "versionInfoPath" ), LogToConsole ) : new PatchUpdater( GetArgument( "versionInfoPath" ) );
			if( patchUpdater.UpdateDownloadLinks( GetArgument( "linksPath" ) ) )
			{
				patchUpdater.SaveChanges();
				Console.WriteLine( "Successful" );
			}
			else
				Console.WriteLine( "Failed" );
		}

		private static void SignXML()
		{
			XMLSigner.SignXMLFile( GetArgument( "xml" ), File.ReadAllText( GetArgument( "key" ) ) );
		}

		private static void VerifyXML()
		{
			Console.WriteLine( "Result: " + XMLSigner.VerifyXMLFile( GetArgument( "xml" ), File.ReadAllText( GetArgument( "key" ) ) ) );
		}

		private static void GenerateRSAKeyPair()
		{
			string privateKey, publicKey;
			SecurityUtils.CreateRSAKeyPair( out publicKey, out privateKey );

			File.WriteAllText( GetArgument( "private" ), privateKey );
			File.WriteAllText( GetArgument( "public" ), publicKey );
		}

		private static void WaitForProjectManager( ProjectManager project )
		{
			while( project.IsRunning )
			{
				Thread.Sleep( 100 );
				LogProjectManagerToConsole( project );
			}

			LogProjectManagerToConsole( project );
		}

		private static void WaitForPatchCreator( PatchCreator patchCreator )
		{
			while( patchCreator.IsRunning )
			{
				Thread.Sleep( 100 );
				LogPatchCreatorToConsole( patchCreator );
			}

			LogPatchCreatorToConsole( patchCreator );
		}

		private static void WaitForPatcher( SimplePatchTool patcher )
		{
			while( patcher.IsRunning )
			{
				Thread.Sleep( 100 );
				LogPatcherToConsole( patcher );
			}

			LogPatcherToConsole( patcher );
		}

		private static void LogProjectManagerToConsole( ProjectManager project )
		{
			string log = project.FetchLog();
			while( log != null )
			{
				LogToConsole( log );
				log = project.FetchLog();
			}
		}

		private static void LogPatchCreatorToConsole( PatchCreator patchCreator )
		{
			string log = patchCreator.FetchLog();
			while( log != null )
			{
				LogToConsole( log );
				log = patchCreator.FetchLog();
			}
		}

		private static void LogPatcherToConsole( SimplePatchTool patcher )
		{
			string log = patcher.FetchLog();
			while( log != null )
			{
				LogToConsole( log );
				log = patcher.FetchLog();
			}

			IOperationProgress progress = patcher.FetchProgress();
			if( progress != null )
				LogToConsole( progress.ProgressInfo );
		}

		private static void LogToConsole( string log )
		{
			if( log.StartsWith( "..." ) )
				Console.WriteLine( "\n" + log );
			else
				Console.WriteLine( log );
		}

		private static string GetArgument( string argument )
		{
			string searchFor = "-" + argument + "=";
			for( int i = 0; i < args.Length; i++ )
			{
				string arg = args[i];
				if( arg.StartsWith( searchFor ) && arg.Length > searchFor.Length )
				{
					string result = arg.Substring( searchFor.Length );
					if( result.Length >= 2 && ( ( result[0] == '"' && result[result.Length - 1] == '"' ) || ( result[0] == '\'' && result[result.Length - 1] == '\'' ) ) )
						result = result.Substring( 1, result.Length - 2 );

					return result;
				}
			}

			return null;
		}

		private static bool HasArgument( string argument )
		{
			string searchFor = "-" + argument;
			for( int i = 0; i < args.Length; i++ )
			{
				if( args[i] == searchFor ||
					( args[i].Length > searchFor.Length && args[i][searchFor.Length] == '=' && args[i].Substring( 0, searchFor.Length ) == searchFor ) )
					return true;
			}

			return false;
		}
	}
}