using System;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Unity.Build;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Publishes executeMethod runner milestones as build progress frames. </summary>
    internal sealed class BuildRunExecuteMethodProgressSink : IBuildExecuteMethodProgressSink
    {
        private readonly UnityIpcBuildRunProgressSink? progressSink;
        private readonly BuildRunExecutionRequest.ExplicitExecuteMethod request;

        /// <summary> Initializes a new instance of the <see cref="BuildRunExecuteMethodProgressSink" /> class. </summary>
        public BuildRunExecuteMethodProgressSink (
            UnityIpcBuildRunProgressSink? progressSink,
            BuildRunExecutionRequest.ExplicitExecuteMethod request)
        {
            this.progressSink = progressSink;
            this.request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <inheritdoc />
        public void OnRunnerResolved ()
        {
            Publish(
                BuildRunProgressEventNames.RunnerResolved,
                BuildRunProgressPhase.RunnerResolution,
                runnerStatus: null);
        }

        /// <inheritdoc />
        public void OnRunnerStarted ()
        {
            Publish(
                BuildRunProgressEventNames.RunnerStarted,
                BuildRunProgressPhase.RunnerInvocation,
                runnerStatus: null);
        }

        /// <inheritdoc />
        public void OnRunnerCompleted (IpcBuildRunnerResultArtifact runnerResult)
        {
            if (runnerResult == null)
            {
                throw new ArgumentNullException(nameof(runnerResult));
            }

            // NOTE: BuildRunUnityIpcMethodHandler publishes completion after log entry replay so
            // runner-window logs always precede the terminal runner event.
        }

        private void Publish (
            string eventName,
            BuildRunProgressPhase phase,
            IpcBuildReportResult? runnerStatus)
        {
            if (progressSink == null)
            {
                return;
            }

            progressSink.Publish(
                eventName,
                new BuildProgressEntry(
                    RunId: request.RunId,
                    ProfileDigest: request.ProfileDigest,
                    Phase: phase,
                    RunnerKind: request.RunnerKind,
                    RunnerStatus: runnerStatus,
                    Verdict: null,
                    ReportRefs: Array.Empty<BuildArtifactKind>(),
                    ErrorCode: null));
        }
    }
}
