using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>ops.read</c> IPC method requests. </summary>
    internal sealed class OpsReadUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly UcliOperationCatalogSnapshot operationCatalogSnapshot;

        private readonly IUnityEditorReadinessGate readinessGate;

        /// <summary> Initializes a new instance of the <see cref="OpsReadUnityIpcMethodHandler" /> class. </summary>
        /// <param name="operationCatalogSnapshot"> The shared discovered operation snapshot. </param>
        /// <param name="readinessGate"> The editor-readiness gate dependency. </param>
        public OpsReadUnityIpcMethodHandler (
            UcliOperationCatalogSnapshot operationCatalogSnapshot,
            IUnityEditorReadinessGate readinessGate)
        {
            this.operationCatalogSnapshot = operationCatalogSnapshot ?? throw new ArgumentNullException(nameof(operationCatalogSnapshot));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.OpsRead;

        /// <inheritdoc />
        public async ValueTask<IpcResponse> Handle (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeOpsReadRequest(
                    request,
                    out IpcOpsReadRequest? payload,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            if (payload!.RequireReadinessGate)
            {
                var readinessResult = await readinessGate.EnsureExecutionReady(payload.FailFast, cancellationToken).ConfigureAwait(false);
                if (!readinessResult.IsReady)
                {
                    var error = readinessResult.Error!;
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        error.Code,
                        error.Message,
                        error.OpId);
                }
            }

            return UnityIpcResponseFactory.CreateSuccessResponse(request, operationCatalogSnapshot.Catalog);
        }
    }
}
