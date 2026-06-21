using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents the result of invoking an executeMethod build runner. </summary>
    internal sealed record BuildExecuteMethodInvocationResult (
        IpcBuildRunnerResultArtifact? RunnerResult,
        IpcBuildReportArtifact? SyntheticReport,
        IpcError? Error)
    {
        public bool IsSuccess => RunnerResult != null && SyntheticReport != null && Error == null;

        public static BuildExecuteMethodInvocationResult Success (
            IpcBuildRunnerResultArtifact runnerResult,
            IpcBuildReportArtifact syntheticReport)
        {
            return new BuildExecuteMethodInvocationResult(runnerResult, syntheticReport, null);
        }

        public static BuildExecuteMethodInvocationResult Failure (IpcError error)
        {
            return new BuildExecuteMethodInvocationResult(null, null, error);
        }
    }
}
