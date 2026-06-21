using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents the result of invoking an executeMethod build runner. </summary>
    internal sealed record BuildExecuteMethodInvocationResult (
        IpcBuildRunnerResultArtifact? RunnerResult,
        IpcError? Error)
    {
        public bool IsSuccess => RunnerResult != null && Error == null;

        public static BuildExecuteMethodInvocationResult Success (IpcBuildRunnerResultArtifact runnerResult)
        {
            return new BuildExecuteMethodInvocationResult(runnerResult, null);
        }

        public static BuildExecuteMethodInvocationResult Failure (IpcError error)
        {
            return new BuildExecuteMethodInvocationResult(null, error);
        }
    }
}
