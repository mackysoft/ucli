using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements method-based dispatch for authorized Unity IPC requests. </summary>
    internal sealed class UnityIpcMethodDispatcher : IUnityIpcMethodDispatcher
    {
        private readonly IExecuteRequestDispatcher executeRequestDispatcher;

        private readonly IServerVersionProvider serverVersionProvider;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcMethodDispatcher" /> class. </summary>
        /// <param name="executeRequestDispatcher"> The execute-request dispatcher dependency. </param>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
        public UnityIpcMethodDispatcher (
            IExecuteRequestDispatcher executeRequestDispatcher,
            IServerVersionProvider serverVersionProvider)
        {
            this.executeRequestDispatcher = executeRequestDispatcher ?? throw new ArgumentNullException(nameof(executeRequestDispatcher));
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
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
        private IpcResponse HandlePing (IpcRequest request)
        {
            if (!UnityIpcMethodDispatcherCodec.TryDecodePingRequest(
                    request,
                    out IpcPingRequest _,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var payload = UnityIpcMethodDispatcherCodec.CreatePingResponsePayload(
                Application.unityVersion,
                serverVersionProvider.GetVersion(),
                EditorApplication.isCompiling);
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
            if (!UnityIpcMethodDispatcherCodec.TryDecodeExecuteRequest(
                    request,
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
            if (!UnityIpcMethodDispatcherCodec.TryDecodeShutdownRequest(
                    request,
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
    }
}
