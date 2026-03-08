using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>ops.read</c> IPC method requests. </summary>
    internal sealed class OpsReadUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly UcliOperationCatalogSnapshot operationCatalogSnapshot;

        /// <summary> Initializes a new instance of the <see cref="OpsReadUnityIpcMethodHandler" /> class. </summary>
        /// <param name="operationCatalogSnapshot"> The shared discovered operation snapshot. </param>
        public OpsReadUnityIpcMethodHandler (UcliOperationCatalogSnapshot operationCatalogSnapshot)
        {
            this.operationCatalogSnapshot = operationCatalogSnapshot ?? throw new ArgumentNullException(nameof(operationCatalogSnapshot));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.OpsRead;

        /// <inheritdoc />
        public ValueTask<IpcResponse> Handle (
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
                    out IpcOpsReadRequest? _,
                    out var errorResponse))
            {
                return new ValueTask<IpcResponse>(errorResponse!);
            }

            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, operationCatalogSnapshot.Catalog));
        }
    }
}
