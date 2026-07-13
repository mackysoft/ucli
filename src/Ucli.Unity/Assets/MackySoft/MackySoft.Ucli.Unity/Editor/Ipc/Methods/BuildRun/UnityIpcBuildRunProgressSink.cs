using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Serializes Unity build-run progress events onto one IPC stream frame writer. </summary>
    internal sealed class UnityIpcBuildRunProgressSink
    {
        private readonly Guid runId;
        private readonly UnityIpcProgressFrameQueue progressFrameQueue;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcBuildRunProgressSink" /> class. </summary>
        public UnityIpcBuildRunProgressSink (
            IIpcStreamFrameWriter streamWriter,
            Guid runId,
            CancellationToken executionCancellationToken)
        {
            this.runId = runId;
            progressFrameQueue = new UnityIpcProgressFrameQueue(
                streamWriter,
                executionCancellationToken,
                BuildRunProgressEventNames.Diagnostic,
                CreateOverflowDiagnostic);
        }

        /// <summary> Queues one progress frame for IPC streaming. </summary>
        public void Publish (
            string eventName,
            object payload)
        {
            progressFrameQueue.Publish(eventName, payload);
        }

        /// <summary> Stops accepting progress frames and waits until all previously accepted frames have been written. </summary>
        public Task CompleteAndFlushAsync (CancellationToken cancellationToken)
        {
            return progressFrameQueue.CompleteAndFlushAsync(cancellationToken);
        }

        private object CreateOverflowDiagnostic ()
        {
            return new BuildDiagnosticEntry(
                runId,
                "BUILD_PROGRESS_DROPPED",
                IpcExecuteDiagnosticSeverityNames.Warning,
                "Build progress entries exceeded the pending IPC frame limit; additional progress entries were dropped.",
                ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerInvocation));
        }
    }
}
