using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Authorizes and dispatches Unity IPC request methods. </summary>
    internal sealed class UnityIpcRequestHandler : IUnityIpcRequestHandler
    {
        private readonly ISessionTokenValidator sessionTokenValidator;
        private readonly IUnityIpcMethodDispatcher methodDispatcher;
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcRequestHandler" /> class. </summary>
        /// <param name="sessionTokenValidator"> The session-token validator dependency. </param>
        /// <param name="methodDispatcher"> The method dispatcher dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
        public UnityIpcRequestHandler (
            ISessionTokenValidator sessionTokenValidator,
            IUnityIpcMethodDispatcher methodDispatcher,
            IDaemonLogger daemonLogger = null)
        {
            this.sessionTokenValidator = sessionTokenValidator ?? throw new ArgumentNullException(nameof(sessionTokenValidator));
            this.methodDispatcher = methodDispatcher ?? throw new ArgumentNullException(nameof(methodDispatcher));
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <summary> Handles one IPC request with strict session-token authorization and method dispatching. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The IPC response envelope. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public async Task<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken = default)
        {
            var validationErrorResponse = await ValidateCommonAsync(request, cancellationToken);
            if (validationErrorResponse != null)
            {
                return validationErrorResponse;
            }

            if (!ContractLiteralCodec.TryParse<IpcResponseMode>(request.ResponseMode, out var responseMode)
                || responseMode != IpcResponseMode.Single)
            {
                return CreateResponseModeMismatchResponse(request, IpcResponseMode.Single);
            }

            return await methodDispatcher.DispatchAsync(request, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IpcResponse> HandleStreamingAsync (
            IpcRequest request,
            IUnityIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken = default)
        {
            if (streamWriter == null)
            {
                throw new ArgumentNullException(nameof(streamWriter));
            }

            var validationErrorResponse = await ValidateCommonAsync(request, cancellationToken);
            if (validationErrorResponse != null)
            {
                return validationErrorResponse;
            }

            if (!ContractLiteralCodec.TryParse<IpcResponseMode>(request.ResponseMode, out var responseMode)
                || responseMode != IpcResponseMode.Stream)
            {
                return CreateResponseModeMismatchResponse(request, IpcResponseMode.Stream);
            }

            return await methodDispatcher.DispatchStreamingAsync(request, streamWriter, cancellationToken);
        }

        private async Task<IpcResponse> ValidateCommonAsync (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(request.SessionToken))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Auth,
                    "Session token is required but missing.",
                    $"requestId={request.RequestId}, method={request.Method}");
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcSessionErrorCodes.SessionTokenRequired,
                    "Session token is required.",
                    null);
            }

            bool tokenAccepted;
            try
            {
                tokenAccepted = await sessionTokenValidator.ValidateAsync(request.SessionToken, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Auth,
                    "Session token validation failed.",
                    exception);
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Session token validation failed. {exception.Message}",
                    null);
            }

            if (!tokenAccepted)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Auth,
                    "Session token validation rejected request.",
                    $"requestId={request.RequestId}, method={request.Method}");
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "Session token is invalid.",
                    null);
            }

            if (request.ProtocolVersion != IpcProtocol.CurrentVersion)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Auth,
                    $"Protocol version mismatch. requested={request.ProtocolVersion}, supported={IpcProtocol.CurrentVersion}.",
                    $"requestId={request.RequestId}, method={request.Method}");
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcProtocolErrorCodes.ProtocolVersionMismatch,
                    $"Protocol version mismatch. Requested={request.ProtocolVersion}, Supported={IpcProtocol.CurrentVersion}.",
                    null);
            }

            if (!ContractLiteralCodec.IsDefined<IpcResponseMode>(request.ResponseMode))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"Unsupported IPC response mode. responseMode={request.ResponseMode}.",
                    $"requestId={request.RequestId}, method={request.Method}");
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    $"Unsupported IPC response mode: {request.ResponseMode}.",
                    null);
            }

            return null;
        }

        private static IpcResponse CreateResponseModeMismatchResponse (
            IpcRequest request,
            IpcResponseMode expectedResponseMode)
        {
            var expectedLiteral = ContractLiteralCodec.ToValue(expectedResponseMode);
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                $"IPC responseMode must be '{expectedLiteral}' for this request path. Actual: {request.ResponseMode}.",
                null);
        }
    }
}
