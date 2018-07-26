namespace Octodiff.Diagnostics
{
    public interface IProgressReporter
    {
        void ReportProgress(string operation, long currentPosition, long total);
    }
}