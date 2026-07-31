using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Provides Play Mode enter observations and side effects without owning
    /// lifecycle state, checkpoints, or terminal publication.
    /// </summary>
    internal interface IPlayEnterLifecycleExecutionProvider
    {
        UnityEditorObservation CaptureObservation ();

        Task<PlayEnterTransitionExecutionResult> EnterAsync (
            IPlayEnterLifecycleExecutionContext executionContext,
            CancellationToken executionDeadlineCancellationToken);
    }
}
