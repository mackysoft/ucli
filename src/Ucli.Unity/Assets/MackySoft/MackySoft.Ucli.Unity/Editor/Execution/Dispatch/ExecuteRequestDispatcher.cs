using System;
using MackySoft.Ucli.Contracts;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.RequestIdempotency;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Runtime;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Dispatch
{
    /// <summary> Dispatches execute requests to operation-phase execution pipelines. </summary>
    internal sealed class ExecuteRequestDispatcher : IExecuteRequestDispatcher
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        private readonly IExecuteRequestNormalizer requestNormalizer;
        private readonly IOperationPhaseExecutor operationPhaseExecutor;
        private readonly IExecuteRequestIdempotencyCoordinator requestIdempotencyCoordinator;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly IUnityMainThreadRequestExecutor mainThreadRequestExecutor;

        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestDispatcher" /> class. </summary>
        /// <param name="requestNormalizer"> The execute-request normalizer dependency. </param>
        /// <param name="operationPhaseExecutor"> The operation-phase executor dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public ExecuteRequestDispatcher (
            IExecuteRequestNormalizer requestNormalizer,
            IOperationPhaseExecutor operationPhaseExecutor)
            : this(
                requestNormalizer,
                operationPhaseExecutor,
                new ExecuteRequestIdempotencyCoordinator(),
                new PassThroughUnityEditorReadinessGate(),
                new InlineUnityMainThreadRequestExecutor())
        {
        }

        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestDispatcher" /> class. </summary>
        /// <param name="requestNormalizer"> The execute-request normalizer dependency. </param>
        /// <param name="operationPhaseExecutor"> The operation-phase executor dependency. </param>
        /// <param name="readinessGate"> The editor-readiness gate dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public ExecuteRequestDispatcher (
            IExecuteRequestNormalizer requestNormalizer,
            IOperationPhaseExecutor operationPhaseExecutor,
            IUnityEditorReadinessGate readinessGate)
            : this(
                requestNormalizer,
                operationPhaseExecutor,
                new ExecuteRequestIdempotencyCoordinator(),
                readinessGate,
                new InlineUnityMainThreadRequestExecutor())
        {
        }

        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestDispatcher" /> class. </summary>
        /// <param name="requestNormalizer"> The execute-request normalizer dependency. </param>
        /// <param name="operationPhaseExecutor"> The operation-phase executor dependency. </param>
        /// <param name="readinessGate"> The editor-readiness gate dependency. </param>
        /// <param name="mainThreadRequestExecutor"> The Unity main-thread executor dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public ExecuteRequestDispatcher (
            IExecuteRequestNormalizer requestNormalizer,
            IOperationPhaseExecutor operationPhaseExecutor,
            IUnityEditorReadinessGate readinessGate,
            IUnityMainThreadRequestExecutor mainThreadRequestExecutor)
            : this(
                requestNormalizer,
                operationPhaseExecutor,
                new ExecuteRequestIdempotencyCoordinator(),
                readinessGate,
                mainThreadRequestExecutor)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestDispatcher" /> class. </summary>
        /// <param name="requestNormalizer"> The execute-request normalizer dependency. </param>
        /// <param name="operationPhaseExecutor"> The operation-phase executor dependency. </param>
        /// <param name="requestIdempotencyCoordinator"> The request-id idempotency coordinator dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public ExecuteRequestDispatcher (
            IExecuteRequestNormalizer requestNormalizer,
            IOperationPhaseExecutor operationPhaseExecutor,
            IExecuteRequestIdempotencyCoordinator requestIdempotencyCoordinator,
            IUnityEditorReadinessGate readinessGate,
            IUnityMainThreadRequestExecutor mainThreadRequestExecutor)
        {
            this.requestNormalizer = requestNormalizer ?? throw new ArgumentNullException(nameof(requestNormalizer));
            this.operationPhaseExecutor = operationPhaseExecutor ?? throw new ArgumentNullException(nameof(operationPhaseExecutor));
            this.requestIdempotencyCoordinator = requestIdempotencyCoordinator ?? throw new ArgumentNullException(nameof(requestIdempotencyCoordinator));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.mainThreadRequestExecutor = mainThreadRequestExecutor ?? throw new ArgumentNullException(nameof(mainThreadRequestExecutor));
        }

        /// <summary> Dispatches one execute request and returns the response envelope. </summary>
        /// <param name="request"> The execute request payload. </param>
        /// <param name="context"> The request-level dispatch context. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The response envelope for the incoming request. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> or <paramref name="context" /> is <see langword="null" />. </exception>
        /// <exception cref="System.OperationCanceledException"> Thrown when dispatch is canceled. </exception>
        public async Task<IpcResponse> DispatchAsync (
            IpcExecuteRequest request,
            ExecuteDispatchContext context,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (request.Arguments.ValueKind != JsonValueKind.Object)
            {
                return ExecuteResponseBuilder.CreateErrorResponse(
                    context,
                    UcliCoreErrorCodes.InvalidArgument,
                    "Request arguments must be a JSON object.",
                    null,
                    SerializerOptions);
            }

            var requestFingerprint = ExecuteRequestFingerprintCalculator.Create(request);
            return await requestIdempotencyCoordinator.ExecuteAsync(
                    context.RequestId,
                    requestFingerprint,
                    _ => DispatchCoreAsync(request, context, cancellationToken),
                    () => ExecuteResponseBuilder.CreateErrorResponse(
                        context,
                        ExecuteRequestErrorCodes.RequestIdConflict,
                        "Request id conflict. The same requestId was already used for a different request content.",
                        null,
                        SerializerOptions),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary> Executes one dispatch flow without idempotency coordination. </summary>
        /// <param name="request"> The execute request payload. </param>
        /// <param name="context"> The request-level dispatch context. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The response envelope for the incoming request. </returns>
        /// <exception cref="System.OperationCanceledException"> Thrown when dispatch is canceled. </exception>
        private async Task<IpcResponse> DispatchCoreAsync (
            IpcExecuteRequest request,
            ExecuteDispatchContext context,
            CancellationToken cancellationToken)
        {
            if (!ExecuteRequestCommandResolver.TryResolve(request.Command, out PhaseExecutionCommand executionCommand))
            {
                return ExecuteResponseBuilder.CreateErrorResponse(
                    context,
                    UcliCoreErrorCodes.CommandNotImplemented,
                    $"Execute command '{request.Command}' is not implemented.",
                    null,
                    SerializerOptions);
            }

            if (request.AllowPlayMode
                && !IsPlayModeMutationCommand(request.Command))
            {
                return ExecuteResponseBuilder.CreateErrorResponse(
                    context,
                    UcliCoreErrorCodes.InvalidArgument,
                    "allowPlayMode is supported only for plan and call execute commands.",
                    null,
                    SerializerOptions);
            }

            var normalizationResult = requestNormalizer.Normalize(request, cancellationToken);
            if (!normalizationResult.IsSuccess)
            {
                var normalizationError = normalizationResult.Error!;
                return ExecuteResponseBuilder.CreateErrorResponse(
                    context,
                    normalizationError.Code,
                    normalizationError.Message,
                    normalizationError.OpId,
                    SerializerOptions);
            }

            var readinessResult = await readinessGate.EnsureExecutionReadyAsync(request.FailFast, cancellationToken, request.AllowPlayMode).ConfigureAwait(false);
            if (!readinessResult.IsReady)
            {
                var lifecycleError = readinessResult.Error!;
                return ExecuteResponseBuilder.CreateErrorResponse(
                    context,
                    lifecycleError.Code,
                    lifecycleError.Message,
                    lifecycleError.OpId,
                    SerializerOptions);
            }

            try
            {
                return await mainThreadRequestExecutor.ExecuteAsync(async () =>
                {
                    var trace = await operationPhaseExecutor.ExecuteAsync(executionCommand, normalizationResult.Request!, cancellationToken);
                    return ExecuteResponseBuilder.CreateExecutionResponse(context, trace, SerializerOptions);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return ExecuteResponseBuilder.CreateErrorResponse(
                    context,
                    UcliCoreErrorCodes.InternalError,
                    $"Unexpected error occurred while dispatching execute request. {exception.Message}",
                    null,
                    SerializerOptions);
            }
        }

        private sealed class PassThroughUnityEditorReadinessGate : IUnityEditorReadinessGate
        {
            public UnityEditorLifecycleSnapshot CaptureSnapshot ()
            {
                return new UnityEditorLifecycleSnapshot(
                    EditorMode: DaemonEditorMode.Batchmode,
                    LifecycleState: IpcEditorLifecycleStateCodec.Ready,
                    BlockingReason: null,
                    CompileState: IpcCompileStateCodec.Ready,
                    CompileGeneration: "0",
                    DomainReloadGeneration: "0",
                    CanAcceptExecutionRequests: true);
            }

            public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
                bool failFast,
                CancellationToken cancellationToken = default,
                bool allowPlayMode = false)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(UnityEditorExecutionReadinessResult.Ready(CaptureSnapshot()));
            }
        }

        private sealed class InlineUnityMainThreadRequestExecutor : IUnityMainThreadRequestExecutor
        {
            public Task<T> ExecuteAsync<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (workItem == null)
                {
                    throw new ArgumentNullException(nameof(workItem));
                }

                return workItem();
            }
        }

        private static bool IsPlayModeMutationCommand (string commandName)
        {
            if (!UcliCommand.TryCreate(commandName, out var command))
            {
                return false;
            }

            return command == UcliCommandIds.Plan
                   || command == UcliCommandIds.Call;
        }
    }
}
