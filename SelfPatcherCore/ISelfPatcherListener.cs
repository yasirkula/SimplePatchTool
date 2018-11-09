namespace SelfPatcherCore
{
	public interface ISelfPatcherListener
	{
		void OnLogAppeared( string message );
		void OnProgressChanged( int currentInstruction, int numberOfInstructions );
		void OnFail( string message );
		void OnSuccess();
	}
}