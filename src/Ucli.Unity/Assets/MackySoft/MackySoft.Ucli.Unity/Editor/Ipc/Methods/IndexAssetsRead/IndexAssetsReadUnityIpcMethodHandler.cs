using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Index;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>index.assets.read</c> IPC method requests. </summary>
    internal sealed class IndexAssetsReadUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly IAssetLookupSnapshotBuilder assetLookupSnapshotBuilder;

        /// <summary> Initializes a new instance of the <see cref="IndexAssetsReadUnityIpcMethodHandler" /> class. </summary>
        public IndexAssetsReadUnityIpcMethodHandler (IAssetLookupSnapshotBuilder assetLookupSnapshotBuilder)
        {
            this.assetLookupSnapshotBuilder = assetLookupSnapshotBuilder ?? throw new ArgumentNullException(nameof(assetLookupSnapshotBuilder));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.IndexAssetsRead;

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

            if (!UnityIpcRequestCodec.TryDecodeIndexAssetsReadRequest(
                    request,
                    out IpcIndexAssetsReadRequest? _,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            try
            {
                var responsePayload = await assetLookupSnapshotBuilder.Build(cancellationToken);
                return UnityIpcResponseFactory.CreateSuccessResponse(request, responsePayload);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcErrorCodes.InvalidArgument,
                    exception.Message,
                    null);
            }
            catch (Exception exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcErrorCodes.InternalError,
                    $"Asset lookup snapshot read failed. {exception.Message}",
                    null);
            }
        }
    }
}
