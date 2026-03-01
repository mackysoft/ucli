using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Authorizes and dispatches Unity IPC request methods. </summary>
    internal sealed class UnityIpcRequestHandler : IUnityIpcRequestHandler
    {
        private readonly ISessionTokenValidator sessionTokenValidator;
        private readonly IUnityIpcMethodDispatcher methodDispatcher;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcRequestHandler" /> class. </summary>
        /// <param name="sessionTokenValidator"> The session-token validator dependency. </param>
        /// <param name="methodDispatcher"> The method dispatcher dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
        public UnityIpcRequestHandler (
            ISessionTokenValidator sessionTokenValidator,
            IUnityIpcMethodDispatcher methodDispatcher)
        {
            this.sessionTokenValidator = sessionTokenValidator ?? throw new ArgumentNullException(nameof(sessionTokenValidator));
            this.methodDispatcher = methodDispatcher ?? throw new ArgumentNullException(nameof(methodDispatcher));
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

            return await methodDispatcher.Dispatch(request, cancellationToken);
        }
    }
}
