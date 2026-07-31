using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Provides Play Mode exit observations and side effects without owning
    /// lifecycle state, checkpoints, or terminal publication.
    /// </summary>
    internal interface IPlayExitLifecycleExecutionProvider
    {
        UnityEditorObservation CaptureObservation ();

        PlayExitTransitionPreparation Prepare (
            CancellationToken executionDeadlineCancellationToken);

        Task<PlayExitTransitionExecutionResult> IssueAsync (
            UnityEditorObservation before,
            CancellationToken executionDeadlineCancellationToken);

        Task<PlayExitTransitionExecutionResult> RecoverAsync (
            UnityEditorObservation before,
            CancellationToken executionDeadlineCancellationToken);
    }
}
