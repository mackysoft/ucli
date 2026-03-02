using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements method-based dispatch for authorized Unity IPC requests. </summary>
    internal sealed class UnityIpcMethodDispatcher : IUnityIpcMethodDispatcher
    {
        private readonly IExecuteRequestDispatcher executeRequestDispatcher;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcMethodDispatcher" /> class. </summary>
        /// <param name="executeRequestDispatcher"> The execute-request dispatcher dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
        public UnityIpcMethodDispatcher (IExecuteRequestDispatcher executeRequestDispatcher)
        {
            this.executeRequestDispatcher = executeRequestDispatcher ?? throw new ArgumentNullException(nameof(executeRequestDispatcher));
        }

        /// <summary> Dispatches one IPC request envelope by method contract. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The response envelope for the request. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public async Task<IpcResponse> Dispatch (
            IpcRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                switch (request.Method)
                {
                    case IpcMethodNames.Ping:
                        return HandlePing(request);

                    case IpcMethodNames.Execute:
                        return await HandleExecute(request, cancellationToken);

                    case IpcMethodNames.Shutdown:
                        return HandleShutdown(request);

                    default:
                        return UnityIpcResponseFactory.CreateErrorResponse(
                            request,
                            IpcErrorCodes.IpcMethodNotSupported,
                            $"IPC method is not supported: {request.Method}.",
                            null);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcErrorCodes.InternalError,
                    $"Unexpected error occurred while handling IPC request. {exception.Message}",
                    null);
            }
        }

        /// <summary> Handles one <c>ping</c> request. </summary>
        /// <param name="request"> The incoming request envelope. </param>
        /// <returns> The response envelope. </returns>
        private static IpcResponse HandlePing (IpcRequest request)
        {
            if (!TryDeserializePayload(
                    request,
                    "Ping",
                    out IpcPingRequest _,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var payload = new IpcPingResponse(
                ServerVersion: "ucli-unity-daemon",
                Runtime: "batchmode",
                UnityVersion: Application.unityVersion);
            return UnityIpcResponseFactory.CreateSuccessResponse(request, payload);
        }

        /// <summary> Handles one <c>execute</c> request. </summary>
        /// <param name="request"> The incoming request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token for dispatch. </param>
        /// <returns> The response envelope. </returns>
        private async Task<IpcResponse> HandleExecute (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryDeserializePayload(
                    request,
                    "Execute",
                    out IpcExecuteRequest? executeRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var context = new ExecuteDispatchContext(
                RequestId: request.RequestId,
                ProtocolVersion: request.ProtocolVersion);
            return await executeRequestDispatcher.Dispatch(executeRequest!, context, cancellationToken);
        }

        /// <summary> Handles one <c>shutdown</c> request. </summary>
        /// <param name="request"> The incoming request envelope. </param>
        /// <returns> The response envelope. </returns>
        private IpcResponse HandleShutdown (IpcRequest request)
        {
            if (!TryDeserializePayload(
                    request,
                    "Shutdown",
                    out IpcShutdownRequest _,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var payload = new IpcShutdownResponse(
                Accepted: true,
                Message: "Shutdown request accepted.");
            return UnityIpcResponseFactory.CreateSuccessResponse(request, payload);
        }

        /// <summary> Tries to deserialize one IPC method payload and builds standardized invalid-payload error response when it fails. </summary>
        /// <typeparam name="TPayload"> The payload model type. </typeparam>
        /// <param name="request"> The incoming request envelope. </param>
        /// <param name="methodName"> The method name used for diagnostics. </param>
        /// <param name="payload"> The deserialized payload when operation succeeds. </param>
        /// <param name="errorResponse"> The error response when operation fails; otherwise <see langword="null" />. </param>
        /// <returns> <see langword="true" /> when payload is valid and deserialized; otherwise <see langword="false" />. </returns>
        private static bool TryDeserializePayload<TPayload> (
            IpcRequest request,
            string methodName,
            out TPayload? payload,
            out IpcResponse? errorResponse)
        {
            if (IpcPayloadCodec.TryDeserialize(
                request.Payload,
                out TPayload parsedPayload,
                out var readError))
            {
                payload = parsedPayload;
                errorResponse = null;
                return true;
            }

            payload = default;
            var message = readError.Kind == IpcPayloadReadErrorKind.NullPayload
                ? $"{methodName} payload is null."
                : readError.Message;
            errorResponse = UnityIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.InvalidArgument,
                $"{methodName} payload is invalid. {message}",
                null);
            return false;
        }
    }
}
