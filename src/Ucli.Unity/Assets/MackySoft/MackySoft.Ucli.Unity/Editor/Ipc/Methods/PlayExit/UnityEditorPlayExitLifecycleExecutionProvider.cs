using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Adapts the current Unity Editor Play Mode exit transition runner to the
    /// action-specific lifecycle execution provider port.
    /// </summary>
    internal sealed class UnityEditorPlayExitLifecycleExecutionProvider :
        IPlayExitLifecycleExecutionProvider
    {
        private readonly PlayExitTransitionRunner transitionRunner;

        public UnityEditorPlayExitLifecycleExecutionProvider (
            PlayExitTransitionRunner transitionRunner)
        {
            this.transitionRunner = transitionRunner
                ?? throw new ArgumentNullException(nameof(transitionRunner));
        }

        public UnityEditorObservation CaptureObservation ()
        {
            return transitionRunner.CaptureObservation();
        }

        public PlayExitTransitionPreparation Prepare (
            CancellationToken executionDeadlineCancellationToken)
        {
            return transitionRunner.Prepare(
                executionDeadlineCancellationToken);
        }

        public Task<PlayExitTransitionExecutionResult> IssueAsync (
            UnityEditorObservation before,
            CancellationToken executionDeadlineCancellationToken)
        {
            return transitionRunner.IssueAsync(
                before,
                executionDeadlineCancellationToken);
        }

        public Task<PlayExitTransitionExecutionResult> RecoverAsync (
            UnityEditorObservation before,
            CancellationToken executionDeadlineCancellationToken)
        {
            return transitionRunner.RecoverAsync(
                before,
                executionDeadlineCancellationToken);
        }

    }
}
