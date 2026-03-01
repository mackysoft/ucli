using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Authorizes and dispatches Unity IPC request methods. </summary>
    internal sealed class UnityIpcRequestHandler : IUnityIpcRequestHandler
    {
        private readonly ISessionTokenValidator sessionTokenValidator;
        private readonly IExecuteRequestDispatcher executeRequestDispatcher;
        private readonly Action shutdownSignal;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcRequestHandler" /> class. </summary>
        /// <param name="sessionTokenValidator"> The session-token validator dependency. </param>
        /// <param name="executeRequestDispatcher"> The execute-request dispatcher dependency. </param>
        /// <param name="shutdownSignal"> The callback invoked when shutdown request is accepted. </param>
        /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
        public UnityIpcRequestHandler (
            ISessionTokenValidator sessionTokenValidator,
            IExecuteRequestDispatcher executeRequestDispatcher,
            Action shutdownSignal)
        {
            this.sessionTokenValidator = sessionTokenValidator ?? throw new ArgumentNullException(nameof(sessionTokenValidator));
            this.executeRequestDispatcher = executeRequestDispatcher ?? throw new ArgumentNullException(nameof(executeRequestDispatcher));
            this.shutdownSignal = shutdownSignal ?? throw new ArgumentNullException(nameof(shutdownSignal));
        }

        /// <summary> Handles one IPC request with strict session-token authorization and method dispatching. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The IPC response envelope. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public async Task<IpcResponse> Handle (
            IpcRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(request.SessionToken))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcErrorCodes.SessionTokenRequired,
                    "Session token is required.",
                    null);
            }

            bool tokenAccepted;
            try
            {
                tokenAccepted = await sessionTokenValidator.Validate(request.SessionToken, cancellationToken);
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
                    $"Session token validation failed. {exception.Message}",
                    null);
            }

            if (!tokenAccepted)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcErrorCodes.SessionTokenInvalid,
                    "Session token is invalid.",
                    null);
            }

            if (request.ProtocolVersion != IpcProtocol.CurrentVersion)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcErrorCodes.ProtocolVersionMismatch,
                    $"Protocol version mismatch. Requested={request.ProtocolVersion}, Supported={IpcProtocol.CurrentVersion}.",
                    null);
            }

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
            try
            {
                _ = request.Payload.Deserialize<IpcPingRequest>(UnityIpcSerializerOptions.Default)
                    ?? throw new JsonException("Ping payload is null.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcErrorCodes.InvalidArgument,
                    $"Ping payload is invalid. {exception.Message}",
                    null);
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
            IpcExecuteRequest executeRequest;
            try
            {
                executeRequest = request.Payload.Deserialize<IpcExecuteRequest>(UnityIpcSerializerOptions.Default)
                    ?? throw new JsonException("Execute payload is null.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcErrorCodes.InvalidArgument,
                    $"Execute payload is invalid. {exception.Message}",
                    null);
            }

            var context = new ExecuteDispatchContext(
                RequestId: request.RequestId,
                ProtocolVersion: request.ProtocolVersion);
            return await executeRequestDispatcher.Dispatch(executeRequest, context, cancellationToken);
        }

        /// <summary> Handles one <c>shutdown</c> request. </summary>
        /// <param name="request"> The incoming request envelope. </param>
        /// <returns> The response envelope. </returns>
        private IpcResponse HandleShutdown (IpcRequest request)
        {
            try
            {
                _ = request.Payload.Deserialize<IpcShutdownRequest>(UnityIpcSerializerOptions.Default)
                    ?? throw new JsonException("Shutdown payload is null.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcErrorCodes.InvalidArgument,
                    $"Shutdown payload is invalid. {exception.Message}",
                    null);
            }

            shutdownSignal();
            var payload = new IpcShutdownResponse(
                Accepted: true,
                Message: "Shutdown request accepted.");
            return UnityIpcResponseFactory.CreateSuccessResponse(request, payload);
        }
    }
}
