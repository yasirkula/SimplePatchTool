namespace SelfPatcherCore
{
	public interface ISelfPatcherListener
	{
		void OnLogAppeared( string message );
		void OnFail( string message );
		void OnSuccess();
	}
}