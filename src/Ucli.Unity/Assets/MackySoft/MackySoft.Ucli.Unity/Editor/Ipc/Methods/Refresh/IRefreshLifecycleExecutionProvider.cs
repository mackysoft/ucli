using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Provides the refresh action with Unity observations and side effects.
    /// A different refresh pipeline can replace this port without taking
    /// ownership of lifecycle state, checkpoints, or terminal publication.
    /// </summary>
    internal interface IRefreshLifecycleExecutionProvider
    {
        UnityProjectIdentity Project { get; }

        UnityEditorRuntimeObservation CaptureObservation ();

        UnityEditorObservation CreateLifecycleObservation (
            UnityEditorRuntimeObservation observation);

        IUnityMutationActivity BeginMutation ();

        void RequestRefresh ();

        Task WaitForNextUpdateAsync (CancellationToken cancellationToken);
    }
}
