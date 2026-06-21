using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Receives executeMethod runner progress milestones. </summary>
    internal interface IBuildExecuteMethodProgressSink
    {
        /// <summary> Called after the executeMethod entrypoint has been resolved. </summary>
        void OnRunnerResolved ();

        /// <summary> Called immediately before invoking the executeMethod entrypoint. </summary>
        void OnRunnerStarted ();

        /// <summary> Called after a valid executeMethod runner result has been observed. </summary>
        void OnRunnerCompleted (IpcBuildRunnerResultArtifact runnerResult);
    }
}
