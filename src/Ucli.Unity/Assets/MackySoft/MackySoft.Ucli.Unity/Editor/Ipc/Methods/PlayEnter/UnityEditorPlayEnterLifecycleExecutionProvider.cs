using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Adapts the current Unity Editor Play Mode enter transition runner to the
    /// action-specific lifecycle execution provider port.
    /// </summary>
    internal sealed class UnityEditorPlayEnterLifecycleExecutionProvider :
        IPlayEnterLifecycleExecutionProvider
    {
        private readonly PlayEnterTransitionRunner transitionRunner;

        public UnityEditorPlayEnterLifecycleExecutionProvider (
            PlayEnterTransitionRunner transitionRunner)
        {
            this.transitionRunner = transitionRunner
                ?? throw new ArgumentNullException(nameof(transitionRunner));
        }

        public UnityEditorObservation CaptureObservation ()
        {
            return transitionRunner.CaptureObservation();
        }

        public Task<PlayEnterTransitionExecutionResult> EnterAsync (
            IPlayEnterLifecycleExecutionContext executionContext,
            CancellationToken executionDeadlineCancellationToken)
        {
            return transitionRunner.EnterAsync(
                executionContext,
                executionDeadlineCancellationToken);
        }
    }
}
