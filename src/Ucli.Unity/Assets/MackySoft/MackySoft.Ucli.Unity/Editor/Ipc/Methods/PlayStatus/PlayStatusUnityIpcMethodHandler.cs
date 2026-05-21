using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>play.status</c> IPC method requests. </summary>
    internal sealed class PlayStatusUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly IpcProjectIdentity projectIdentity;
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="PlayStatusUnityIpcMethodHandler" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="readinessGate"> The lifecycle snapshot provider dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this IPC host. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        public PlayStatusUnityIpcMethodHandler (
            IServerVersionProvider serverVersionProvider,
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IDaemonLogger daemonLogger = null)
        {
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.PlayStatus;

        /// <inheritdoc />
        public ValueTask<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodePlayStatusRequest(
                    request,
                    out IpcPlayStatusRequest _,
                    out var errorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Health,
                    "Play status payload decode failed.");
                return new ValueTask<IpcResponse>(errorResponse!);
            }

            var payload = new IpcPlayStatusResponse(CreateSnapshot(readinessGate.CaptureSnapshot()));
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, payload));
        }

        private IpcPlayLifecycleSnapshot CreateSnapshot (UnityEditorLifecycleSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new IpcPlayLifecycleSnapshot(
                ServerVersion: serverVersionProvider.GetVersion(),
                EditorMode: DaemonEditorModeCodec.ToValue(snapshot.EditorMode),
                UnityVersion: projectIdentity.UnityVersion,
                ProjectFingerprint: projectIdentity.ProjectFingerprint,
                LifecycleState: snapshot.LifecycleState,
                BlockingReason: snapshot.BlockingReason,
                CompileState: snapshot.CompileState,
                CompileGeneration: snapshot.CompileGeneration,
                DomainReloadGeneration: snapshot.DomainReloadGeneration,
                CanAcceptExecutionRequests: snapshot.CanAcceptExecutionRequests,
                ObservedAtUtc: snapshot.ObservedAtUtc,
                ActionRequired: snapshot.ActionRequired,
                PrimaryDiagnostic: snapshot.PrimaryDiagnostic,
                PlayMode: snapshot.PlayMode);
        }
    }
}
