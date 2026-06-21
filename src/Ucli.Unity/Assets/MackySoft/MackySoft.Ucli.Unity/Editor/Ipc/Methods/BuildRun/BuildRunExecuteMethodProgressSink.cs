using System;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Build;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Publishes executeMethod runner milestones as build progress frames. </summary>
    internal sealed class BuildRunExecuteMethodProgressSink : IBuildExecuteMethodProgressSink
    {
        private readonly UnityIpcBuildRunProgressSink? progressSink;
        private readonly IpcBuildRunRequest request;

        /// <summary> Initializes a new instance of the <see cref="BuildRunExecuteMethodProgressSink" /> class. </summary>
        public BuildRunExecuteMethodProgressSink (
            UnityIpcBuildRunProgressSink? progressSink,
            IpcBuildRunRequest request)
        {
            this.progressSink = progressSink;
            this.request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <inheritdoc />
        public void OnRunnerResolved ()
        {
            Publish(
                BuildRunProgressEventNames.RunnerResolved,
                "runnerResolution",
                runnerStatus: null);
        }

        /// <inheritdoc />
        public void OnRunnerStarted ()
        {
            Publish(
                BuildRunProgressEventNames.RunnerStarted,
                "runnerInvocation",
                runnerStatus: null);
        }

        /// <inheritdoc />
        public void OnRunnerCompleted (IpcBuildRunnerResultArtifact runnerResult)
        {
            if (runnerResult == null)
            {
                throw new ArgumentNullException(nameof(runnerResult));
            }

            Publish(
                BuildRunProgressEventNames.RunnerCompleted,
                "runnerResult",
                runnerResult.Status);
        }

        private void Publish (
            string eventName,
            string phase,
            string? runnerStatus)
        {
            if (progressSink == null)
            {
                return;
            }

            progressSink.Publish(
                eventName,
                new BuildProgressEntry(
                    RunId: request.RunId,
                    ProfileDigest: request.ProfileDigest!,
                    Phase: phase,
                    RunnerKind: request.RunnerKind,
                    RunnerStatus: runnerStatus,
                    Verdict: null,
                    ReportRefs: Array.Empty<string>(),
                    ErrorCode: null));
        }
    }
}
