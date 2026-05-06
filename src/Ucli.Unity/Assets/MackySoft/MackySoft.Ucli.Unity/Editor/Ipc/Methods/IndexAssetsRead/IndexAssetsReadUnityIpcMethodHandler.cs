using System;
using MackySoft.Ucli.Contracts;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Index;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>index.assets.read</c> IPC method requests. </summary>
    internal sealed class IndexAssetsReadUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly IAssetLookupSnapshotBuilder assetLookupSnapshotBuilder;

        private readonly IUnityEditorReadinessGate readinessGate;

        /// <summary> Initializes a new instance of the <see cref="IndexAssetsReadUnityIpcMethodHandler" /> class. </summary>
        public IndexAssetsReadUnityIpcMethodHandler (
            IAssetLookupSnapshotBuilder assetLookupSnapshotBuilder,
            IUnityEditorReadinessGate readinessGate)
        {
            this.assetLookupSnapshotBuilder = assetLookupSnapshotBuilder ?? throw new ArgumentNullException(nameof(assetLookupSnapshotBuilder));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
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
                    out IpcIndexAssetsReadRequest? payload,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var readinessResult = await readinessGate.EnsureExecutionReady(payload!.FailFast, cancellationToken).ConfigureAwait(false);
            if (!readinessResult.IsReady)
            {
                var error = readinessResult.Error!;
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    error.Code,
                    error.Message,
                    error.OpId);
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
                    UcliCoreErrorCodes.InvalidArgument,
                    exception.Message,
                    null);
            }
            catch (Exception exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Asset lookup snapshot read failed. {exception.Message}",
                    null);
            }
        }
    }
}
