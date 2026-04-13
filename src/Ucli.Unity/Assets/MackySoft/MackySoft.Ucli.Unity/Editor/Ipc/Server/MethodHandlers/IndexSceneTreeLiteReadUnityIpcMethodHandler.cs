using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>index.scene-tree-lite.read</c> IPC method requests. </summary>
    internal sealed class IndexSceneTreeLiteReadUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly ISceneTreeLiteSnapshotBuilder sceneTreeLiteSnapshotBuilder;

        /// <summary> Initializes a new instance of the <see cref="IndexSceneTreeLiteReadUnityIpcMethodHandler" /> class. </summary>
        public IndexSceneTreeLiteReadUnityIpcMethodHandler (ISceneTreeLiteSnapshotBuilder sceneTreeLiteSnapshotBuilder)
        {
            this.sceneTreeLiteSnapshotBuilder = sceneTreeLiteSnapshotBuilder ?? throw new ArgumentNullException(nameof(sceneTreeLiteSnapshotBuilder));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.IndexSceneTreeLiteRead;

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

            if (!UnityIpcRequestCodec.TryDecodeIndexSceneTreeLiteReadRequest(
                    request,
                    out IpcIndexSceneTreeLiteReadRequest? payload,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            try
            {
                var responsePayload = await sceneTreeLiteSnapshotBuilder.Build(payload!.ScenePath, cancellationToken);
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
                    $"Scene-tree-lite snapshot read failed. {exception.Message}",
                    null);
            }
        }
    }
}
