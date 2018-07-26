namespace Octodiff.Diagnostics
{
    public class NullProgressReporter : IProgressReporter
    {
        public void ReportProgress(string operation, long currentPosition, long total)
        {
        }
    }
}