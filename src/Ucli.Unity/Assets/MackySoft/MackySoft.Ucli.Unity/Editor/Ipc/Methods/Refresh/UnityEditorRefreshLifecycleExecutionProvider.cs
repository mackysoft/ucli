using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Owns the refresh-specific ready admission performed immediately before a new Start Record is persisted.
    /// </summary>
    internal sealed class RefreshLifecycleExecutionStartAdmissionPolicy :
        ILifecycleExecutionStartAdmissionPolicy
    {
        private readonly IUnityEditorReadinessGate readinessGate;

        public RefreshLifecycleExecutionStartAdmissionPolicy (
            IUnityEditorReadinessGate readinessGate)
        {
            this.readinessGate = readinessGate
                ?? throw new ArgumentNullException(nameof(readinessGate));
        }

        public LifecycleExecutionKind Kind => LifecycleExecutionKind.Refresh;

        public async ValueTask<LifecycleExecutionStartAdmission> AdmitAsync (
            DateTimeOffset deadlineUtc,
            CancellationToken cancellationToken)
        {
            if (deadlineUtc <= DateTimeOffset.UtcNow)
            {
                return CreateDeadlineRejection();
            }

            // The caller performs the waitable preflight. This immediate provider-side check closes
            // the readiness race between its final observation and durable Start persistence.
            var readinessResult = await readinessGate.EnsureExecutionReadyAsync(
                failFast: true,
                cancellationToken);
            if (!readinessResult.IsReady)
            {
                return LifecycleExecutionStartAdmission.Rejected(
                    readinessResult.Error);
            }
            if (deadlineUtc <= DateTimeOffset.UtcNow)
            {
                return CreateDeadlineRejection();
            }

            return LifecycleExecutionStartAdmission.Accepted(
                readinessResult.Observation);
        }

        private static LifecycleExecutionStartAdmission CreateDeadlineRejection ()
        {
            return LifecycleExecutionStartAdmission.Rejected(
                new IpcError(
                    IpcTransportErrorCodes.IpcTimeout,
                    "Refresh start admission deadline expired before the Start Record was persisted.",
                    InstancePath: null));
        }
    }

    /// <summary>
    /// Adapts the current Unity Editor asset refresh implementation to the
    /// refresh lifecycle execution provider port.
    /// </summary>
    internal sealed class UnityEditorRefreshLifecycleExecutionProvider :
        IRefreshLifecycleExecutionProvider
    {
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityMutationLaneControl mutationLaneControl;
        private readonly IUnityAssetRefreshController assetRefreshController;
        private readonly IUnityEditorUpdateAwaiter editorUpdateAwaiter;

        public UnityEditorRefreshLifecycleExecutionProvider (
            IUnityEditorReadinessGate readinessGate,
            UnityProjectIdentity project,
            IServerVersionProvider serverVersionProvider,
            IUnityMutationLaneControl mutationLaneControl,
            IUnityAssetRefreshController assetRefreshController,
            IUnityEditorUpdateAwaiter editorUpdateAwaiter)
        {
            this.readinessGate = readinessGate
                ?? throw new ArgumentNullException(nameof(readinessGate));
            Project = project
                ?? throw new ArgumentNullException(nameof(project));
            this.serverVersionProvider = serverVersionProvider
                ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.mutationLaneControl = mutationLaneControl
                ?? throw new ArgumentNullException(nameof(mutationLaneControl));
            this.assetRefreshController = assetRefreshController
                ?? throw new ArgumentNullException(nameof(assetRefreshController));
            this.editorUpdateAwaiter = editorUpdateAwaiter
                ?? throw new ArgumentNullException(nameof(editorUpdateAwaiter));
        }

        public UnityProjectIdentity Project { get; }

        public UnityEditorRuntimeObservation CaptureObservation ()
        {
            return readinessGate.CaptureObservation();
        }

        public UnityEditorObservation CreateLifecycleObservation (
            UnityEditorRuntimeObservation observation)
        {
            return UnityLifecycleResponseFactory.Create(
                Project,
                serverVersionProvider.GetVersion(),
                observation);
        }

        public IUnityMutationActivity BeginMutation ()
        {
            return mutationLaneControl.BeginMutation();
        }

        public void RequestRefresh ()
        {
            assetRefreshController.Refresh();
        }

        public Task WaitForNextUpdateAsync (
            CancellationToken cancellationToken)
        {
            return editorUpdateAwaiter.WaitForNextUpdateAsync(cancellationToken);
        }
    }
}
