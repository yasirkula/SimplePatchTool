using SelfPatcherCore;
using System;

namespace SelfPatcherConsoleApp
{
	public class Program
	{
		private class ConsoleSelfPatcherListener : ISelfPatcherListener
		{
			public SelfPatcher selfPatcher;

			public void OnLogAppeared( string message )
			{
				Console.WriteLine( message );
			}

			public void OnProgressChanged( int currentInstruction, int numberOfInstructions )
			{
				Console.WriteLine( (int) ( currentInstruction * 100f / numberOfInstructions ) + "%" );
			}

			public void OnFail( string message )
			{
				Console.WriteLine( message );
				Console.ReadKey( true );
			}

			public void OnSuccess()
			{
				selfPatcher.ExecutePostSelfPatcher();
			}
		}

		public static void Main( string[] args )
		{
			ConsoleSelfPatcherListener listener = new ConsoleSelfPatcherListener();
			SelfPatcher selfPatcher = new SelfPatcher( listener );
			listener.selfPatcher = selfPatcher;

			selfPatcher.Run( args );
		}
	}
}