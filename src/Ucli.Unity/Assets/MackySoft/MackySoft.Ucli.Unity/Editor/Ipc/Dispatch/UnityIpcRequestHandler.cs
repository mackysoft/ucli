using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;

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
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
        public UnityIpcRequestHandler (
            ISessionTokenValidator sessionTokenValidator,
            IUnityIpcMethodDispatcher methodDispatcher,
            IDaemonLogger daemonLogger)
        {
            this.sessionTokenValidator = sessionTokenValidator ?? throw new ArgumentNullException(nameof(sessionTokenValidator));
            this.methodDispatcher = methodDispatcher ?? throw new ArgumentNullException(nameof(methodDispatcher));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        /// <inheritdoc />
        public async Task<UnityIpcRequestValidationResult> ValidateAsync (
            IpcRequestEnvelope request,
            IpcRequestPhaseScope phaseScope)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (phaseScope == null)
            {
                throw new ArgumentNullException(nameof(phaseScope));
            }

            var hasResponseMode = TextVocabulary.TryGetValue(
                request.ResponseMode,
                out IpcResponseMode responseMode);
            var errorResponseMode = hasResponseMode
                ? responseMode
                : IpcResponseMode.Single;
            try
            {
                return await ValidateRequestAsync(
                    request,
                    hasResponseMode,
                    responseMode,
                    phaseScope.ExecutionCancellation.Token);
            }
            catch (OperationCanceledException) when (
                phaseScope.ExecutionCancellation.Reason
                    == IpcRequestCancellationReason.ExecutionDeadline)
            {
                return UnityIpcRequestValidationResult.Failure(
                    CreateExecutionTimeoutResponse(request),
                    errorResponseMode);
            }
        }

        /// <inheritdoc />
        public async Task<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestPhaseScope phaseScope)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (phaseScope == null)
            {
                throw new ArgumentNullException(nameof(phaseScope));
            }

            if (request.ResponseMode != IpcResponseMode.Single)
            {
                throw new ArgumentException(
                    "Non-streaming request handling requires single response mode.",
                    nameof(request));
            }

            try
            {
                return await methodDispatcher.DispatchAsync(
                    request,
                    phaseScope);
            }
            catch (OperationCanceledException) when (
                phaseScope.ExecutionCancellation.Reason
                    == IpcRequestCancellationReason.ExecutionDeadline)
            {
                return CreateExecutionTimeoutResponse(request);
            }
        }

        /// <inheritdoc />
        public async Task<IpcResponse> HandleStreamingAsync (
            ValidatedUnityIpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            IpcRequestPhaseScope phaseScope)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (streamWriter == null)
            {
                throw new ArgumentNullException(nameof(streamWriter));
            }

            if (phaseScope == null)
            {
                throw new ArgumentNullException(nameof(phaseScope));
            }

            if (request.ResponseMode != IpcResponseMode.Stream)
            {
                throw new ArgumentException(
                    "Streaming request handling requires stream response mode.",
                    nameof(request));
            }

            try
            {
                return await methodDispatcher.DispatchStreamingAsync(
                    request,
                    streamWriter,
                    phaseScope);
            }
            catch (OperationCanceledException) when (
                phaseScope.ExecutionCancellation.Reason
                    == IpcRequestCancellationReason.ExecutionDeadline)
            {
                return CreateExecutionTimeoutResponse(request);
            }
        }

        private async Task<UnityIpcRequestValidationResult> ValidateRequestAsync (
            IpcRequestEnvelope request,
            bool hasResponseMode,
            IpcResponseMode responseMode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var errorResponseMode = hasResponseMode
                ? responseMode
                : IpcResponseMode.Single;

            if (string.IsNullOrWhiteSpace(request.SessionToken))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Auth,
                    "Session token is required but missing.",
                    $"requestId={request.RequestId}");
                return UnityIpcRequestValidationResult.Failure(
                    UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcSessionErrorCodes.SessionTokenRequired,
                        "Session token is required.",
                        null),
                    errorResponseMode);
            }

            if (!IpcSessionToken.TryParse(request.SessionToken, out var sessionToken))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Auth,
                    "Session token has an invalid format.",
                    $"requestId={request.RequestId}");
                return UnityIpcRequestValidationResult.Failure(
                    UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcSessionErrorCodes.SessionTokenInvalid,
                        "Session token is invalid.",
                        null),
                    errorResponseMode);
            }

            bool tokenAccepted;
            try
            {
                tokenAccepted = await sessionTokenValidator.ValidateAsync(sessionToken, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
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
                return UnityIpcRequestValidationResult.Failure(
                    UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        UcliCoreErrorCodes.InternalError,
                        "Session token validation failed.",
                        null),
                    errorResponseMode);
            }

            if (!tokenAccepted)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Auth,
                    "Session token validation rejected request.",
                    $"requestId={request.RequestId}");
                return UnityIpcRequestValidationResult.Failure(
                    UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcSessionErrorCodes.SessionTokenInvalid,
                        "Session token is invalid.",
                        null),
                    errorResponseMode);
            }

            if (request.ProtocolVersion != IpcProtocol.CurrentVersion)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Auth,
                    $"Protocol version mismatch. requested={request.ProtocolVersion}, supported={IpcProtocol.CurrentVersion}.",
                    $"requestId={request.RequestId}");
                return UnityIpcRequestValidationResult.Failure(
                    UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcProtocolErrorCodes.ProtocolVersionMismatch,
                        $"Protocol version mismatch. Requested={request.ProtocolVersion}, Supported={IpcProtocol.CurrentVersion}.",
                        null),
                    errorResponseMode);
            }

            if (!hasResponseMode)
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Unsupported IPC response mode.",
                    $"requestId={request.RequestId}");
                return UnityIpcRequestValidationResult.Failure(
                    UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        UcliCoreErrorCodes.InvalidArgument,
                        "Unsupported IPC response mode.",
                        null),
                    IpcResponseMode.Single);
            }

            if (!TextVocabulary.TryGetValue(request.Method, out UnityIpcMethod method))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Unsupported Unity IPC method.",
                    $"requestId={request.RequestId}");
                return UnityIpcRequestValidationResult.Failure(
                    UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcProtocolErrorCodes.IpcMethodNotSupported,
                        "Unity IPC method is not supported.",
                        null),
                    responseMode);
            }

            return UnityIpcRequestValidationResult.Success(
                new ValidatedUnityIpcRequest(
                    request.RequestId,
                    method,
                    request.Payload,
                    responseMode,
                    request.RequestDeadlineUtc,
                    request.RequestDeadlineRemainingMilliseconds));
        }

        private static IpcResponse CreateExecutionTimeoutResponse (IIpcRequestCorrelation request)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                IpcTransportErrorCodes.IpcTimeout,
                "Unity IPC request reached its execution cutoff before authorization and method execution completed.",
                null);
        }
    }
}
