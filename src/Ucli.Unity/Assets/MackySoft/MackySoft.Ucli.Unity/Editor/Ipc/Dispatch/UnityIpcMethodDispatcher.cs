using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements method-based dispatch for authorized Unity IPC requests. </summary>
    internal sealed class UnityIpcMethodDispatcher : IUnityIpcMethodDispatcher
    {
        private readonly IReadOnlyDictionary<UnityIpcMethod, IUnityIpcMethodHandler> methodHandlers;

        private readonly IUnityMainThreadRequestExecutor mutationRequestExecutor;

        private readonly IUnityControlPlaneRequestExecutor controlPlaneRequestExecutor;

        private readonly IRecoverableIpcOperationStore recoverableOperationStore;

        private readonly IDaemonLogger daemonLogger;

        private readonly SemaphoreSlim recoverableDispatchGate = new SemaphoreSlim(1, 1);

        /// <summary> Initializes a new instance of the <see cref="UnityIpcMethodDispatcher" /> class. </summary>
        /// <param name="methodHandlers"> Registered method handlers resolved by DI. </param>
        /// <param name="mutationRequestExecutor"> The serialized executor for Unity mutation requests. </param>
        /// <param name="controlPlaneRequestExecutor"> The independent executor for control-plane requests. </param>
        /// <param name="recoverableOperationStore"> The durable operation store used by recoverable handlers, or <see langword="null" /> when the host does not support recoverable operations. </param>
        /// <param name="daemonLogger"> The daemon logger. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="methodHandlers" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when method handlers are empty or invalid. </exception>
        public UnityIpcMethodDispatcher (
            IEnumerable<IUnityIpcMethodHandler> methodHandlers,
            IUnityMainThreadRequestExecutor mutationRequestExecutor,
            IUnityControlPlaneRequestExecutor controlPlaneRequestExecutor,
            IRecoverableIpcOperationStore recoverableOperationStore,
            IDaemonLogger daemonLogger)
        {
            if (methodHandlers == null)
            {
                throw new ArgumentNullException(nameof(methodHandlers));
            }

            this.methodHandlers = CreateMethodHandlers(methodHandlers);
            this.mutationRequestExecutor = mutationRequestExecutor ?? throw new ArgumentNullException(nameof(mutationRequestExecutor));
            this.controlPlaneRequestExecutor = controlPlaneRequestExecutor ?? throw new ArgumentNullException(nameof(controlPlaneRequestExecutor));
            this.recoverableOperationStore = recoverableOperationStore;
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        /// <summary> Dispatches one validated IPC request by method contract. </summary>
        /// <param name="request"> The authorized and validated Unity IPC request. </param>
        /// <param name="phaseScope"> The connection-owned phase scope for the complete exchange. </param>
        /// <returns> The response envelope for the request. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public async Task<IpcResponse> DispatchAsync (
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

            var requestCancellation = phaseScope.ExecutionCancellation;

            try
            {
                requestCancellation.Token.ThrowIfCancellationRequested();
                if (!methodHandlers.TryGetValue(request.Method, out var methodHandler))
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcProtocolErrorCodes.IpcMethodNotSupported,
                        "Unity IPC method handler is not registered.",
                        null);
                }

                var response = await ExecuteOnSelectedLaneAsync(
                    methodHandler,
                    request,
                    phaseScope)
                    .ConfigureAwait(false);
                return EnsureCorrelatedResponse(request, response);
            }
            catch (OperationCanceledException) when (
                requestCancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
            {
                return CreateExecutionTimeoutResponse(request);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnityMutationLaneUnavailableException exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    EditorLifecycleErrorCodes.EditorBusy,
                    exception.Message,
                    null);
            }
            catch (UnityControlPlaneCapacityExceededException exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    EditorLifecycleErrorCodes.EditorBusy,
                    exception.Message,
                    null);
            }
            catch (Exception exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Unexpected error occurred while handling IPC request. {exception.Message}",
                    null);
            }
        }

        /// <inheritdoc />
        public async Task<IpcResponse> DispatchStreamingAsync (
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

            var requestCancellation = phaseScope.ExecutionCancellation;

            try
            {
                requestCancellation.Token.ThrowIfCancellationRequested();
                if (!methodHandlers.TryGetValue(request.Method, out var methodHandler))
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcProtocolErrorCodes.IpcMethodNotSupported,
                        "Unity IPC method handler is not registered.",
                        null);
                }

                if (!UnityIpcMethodCapabilities.SupportsStreaming(request.Method)
                    || methodHandler is not IStreamingUnityIpcMethodHandler streamingMethodHandler)
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcProtocolErrorCodes.IpcMethodNotSupported,
                        $"IPC method does not support streaming: {request.Method}.",
                        null);
                }

                var response = await ExecuteOnSelectedLaneAsync(
                    streamingMethodHandler,
                    request,
                    streamWriter,
                    phaseScope)
                    .ConfigureAwait(false);
                return EnsureCorrelatedResponse(request, response);
            }
            catch (OperationCanceledException) when (
                requestCancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
            {
                return CreateExecutionTimeoutResponse(request);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnityMutationLaneUnavailableException exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    EditorLifecycleErrorCodes.EditorBusy,
                    exception.Message,
                    null);
            }
            catch (UnityControlPlaneCapacityExceededException exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    EditorLifecycleErrorCodes.EditorBusy,
                    exception.Message,
                    null);
            }
            catch (Exception exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Unexpected error occurred while handling streaming IPC request. {exception.Message}",
                    null);
            }
        }

        private static IpcResponse CreateExecutionTimeoutResponse (ValidatedUnityIpcRequest request)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                IpcTransportErrorCodes.IpcTimeout,
                $"Unity IPC request timed out before method execution reached a terminal state: {request.Method}.",
                null);
        }

        /// <summary> Returns a response correlated to the incoming request. </summary>
        /// <param name="request"> The incoming request. </param>
        /// <param name="response"> The response produced by a method handler or recovery replay. </param>
        /// <returns> The supplied response when correlated; otherwise an internal-error response correlated to <paramref name="request" />. </returns>
        private static IpcResponse EnsureCorrelatedResponse (
            ValidatedUnityIpcRequest request,
            IpcResponse response)
        {
            if (response != null && response.RequestId == request.RequestId)
            {
                return response;
            }

            var actualRequestId = response?.RequestId?.ToString("D") ?? "null";
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InternalError,
                $"IPC method '{request.Method}' returned an uncorrelated response. "
                    + $"Expected requestId={request.RequestId:D}, actual requestId={actualRequestId}.",
                null);
        }

        private Task<IpcResponse> ExecuteOnSelectedLaneAsync (
            IUnityIpcMethodHandler methodHandler,
            ValidatedUnityIpcRequest request,
            IpcRequestPhaseScope phaseScope)
        {
            var cancellation = phaseScope.ExecutionCancellation;
            if (methodHandler is IRecoverableUnityIpcMethodHandler recoverableMethodHandler
                && recoverableOperationStore != null)
            {
                return DispatchRecoverableAsync(
                    recoverableMethodHandler,
                    request,
                    phaseScope);
            }

            var terminalResponseSource = new TaskCompletionSource<IpcResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Func<Task<IpcResponse>> workItem = async () =>
            {
                var response = await methodHandler
                    .HandleAsync(request, cancellation)
                    .ConfigureAwait(false);
                terminalResponseSource.TrySetResult(response);
                return response;
            };

            Task<IpcResponse> laneExecutionTask;
            if (methodHandler is IUnityControlPlaneIpcMethodHandler)
            {
                laneExecutionTask = controlPlaneRequestExecutor.ExecuteAsync(workItem, cancellation.Token);
            }
            else
            {
                laneExecutionTask = mutationRequestExecutor.ExecuteAsync(workItem, cancellation.Token);
            }

            return AwaitLaneExecutionAsync(laneExecutionTask, terminalResponseSource, cancellation);
        }

        private Task<IpcResponse> ExecuteOnSelectedLaneAsync (
            IStreamingUnityIpcMethodHandler methodHandler,
            ValidatedUnityIpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            IpcRequestPhaseScope phaseScope)
        {
            var cancellation = phaseScope.ExecutionCancellation;
            var terminalResponseSource = new TaskCompletionSource<IpcResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Func<Task<IpcResponse>> workItem = async () =>
            {
                var response = await methodHandler
                    .HandleStreamingAsync(request, streamWriter, cancellation)
                    .ConfigureAwait(false);
                terminalResponseSource.TrySetResult(response);
                return response;
            };

            Task<IpcResponse> laneExecutionTask;
            if (methodHandler is IUnityControlPlaneIpcMethodHandler)
            {
                laneExecutionTask = controlPlaneRequestExecutor.ExecuteAsync(workItem, cancellation.Token);
            }
            else
            {
                laneExecutionTask = mutationRequestExecutor.ExecuteAsync(workItem, cancellation.Token);
            }

            return AwaitLaneExecutionAsync(laneExecutionTask, terminalResponseSource, cancellation);
        }

        private static async Task<IpcResponse> AwaitLaneExecutionAsync (
            Task<IpcResponse> laneExecutionTask,
            TaskCompletionSource<IpcResponse> terminalResponseSource,
            IpcRequestCancellation cancellation)
        {
            try
            {
                return await laneExecutionTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline
                && terminalResponseSource.Task.Status == TaskStatus.RanToCompletion)
            {
                var terminalResponse = await terminalResponseSource.Task.ConfigureAwait(false);
                if (IsExecutionDeadlineResponse(terminalResponse))
                {
                    return terminalResponse;
                }

                throw;
            }
        }

        private static bool IsExecutionDeadlineResponse (IpcResponse response)
        {
            if (response == null
                || response.Status != IpcResponseStatus.Error)
            {
                return false;
            }

            for (var index = 0; index < response.Errors.Count; index++)
            {
                var errorCode = response.Errors[index].Code;
                if (errorCode == IpcTransportErrorCodes.IpcTimeout
                    || errorCode == PlayModeErrorCodes.PlayModeTransitionTimeout)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<IpcResponse> DispatchRecoverableAsync (
            IRecoverableUnityIpcMethodHandler methodHandler,
            ValidatedUnityIpcRequest request,
            IpcRequestPhaseScope phaseScope)
        {
            var cancellation = phaseScope.ExecutionCancellation;
            if (!methodHandler.TryCreateRecoverableRequestPayloadHash(
                    request,
                    out var requestPayloadHash,
                    out var hashErrorResponse))
            {
                return hashErrorResponse;
            }

            if (requestPayloadHash == null)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"IPC method '{request.Method}' returned a null recoverable request payload hash.",
                    null);
            }

            await recoverableDispatchGate.WaitAsync(cancellation.Token).ConfigureAwait(false);
            var dispatchLifetime = new RecoverableIpcDispatchLifetime(recoverableDispatchGate);
            RecoverableIpcDispatchLifetime? ownedDispatchLifetime = dispatchLifetime;
            try
            {
                phaseScope.RetainResourcesUntil(dispatchLifetime.Retirement);
                var readResult = await recoverableOperationStore.ReadAsync(
                        methodHandler.Method,
                        request.RequestId,
                        requestPayloadHash,
                        cancellation.Token)
                    .ConfigureAwait(false);
                if (!readResult.IsSuccess)
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        UcliCoreErrorCodes.InternalError,
                        $"Recoverable IPC operation state could not be read. {readResult.ErrorMessage}",
                        null);
                }

                var record = readResult.Record;
                if (record != null
                    && record.State == RecoverableIpcOperationState.Completed
                    && record.Response != null)
                {
                    // NOTE: Replays after response loss must be idempotent. Returning the
                    // completed response prevents the method handler from repeating Unity state changes.
                    return record.Response;
                }

                var context = new RecoverableIpcOperationContext(
                    recoverableOperationStore,
                    methodHandler.Method,
                    request.RequestId,
                    requestPayloadHash,
                    record);
                var terminalResponseSource = new TaskCompletionSource<RecoverableTerminalResponse>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var laneExecutionTask = ExecuteRecoverableHandlerOnSelectedLaneAsync(
                    methodHandler,
                    request,
                    context,
                    terminalResponseSource,
                    cancellation,
                    phaseScope.PersistenceCutoffToken,
                    dispatchLifetime);

                RecoverableTerminalResponse terminalResponse;
                try
                {
                    terminalResponse = await laneExecutionTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    cancellation.Reason != IpcRequestCancellationReason.None
                    &&
                    terminalResponseSource.Task.Status == TaskStatus.RanToCompletion)
                {
                    terminalResponse = await terminalResponseSource.Task.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    if (dispatchLifetime.TryRevokeUnstartedWork())
                    {
                        ownedDispatchLifetime = null;
                    }
                    else
                    {
                        ownedDispatchLifetime = null;
                        _ = RetireAbandonedRecoverableDispatchAsync(
                            terminalResponseSource.Task,
                            dispatchLifetime);
                    }

                    throw;
                }

                var response = terminalResponse.Response;
                IpcResponse completionPersistenceFailureResponse = null;
                if (terminalResponse.CompletionPersistenceTask != null)
                {
                    RecoverableIpcOperationStoreResult completionResult = null;
                    var persistenceCutoffToken = phaseScope.PersistenceCutoffToken;
                    try
                    {
                        var completionTask = terminalResponse.CompletionPersistenceTask;
                        var cutoffTask = Task.Delay(
                            Timeout.Infinite,
                            persistenceCutoffToken);
                        var completedTask = await Task.WhenAny(
                                completionTask,
                                cutoffTask)
                            .ConfigureAwait(false);
                        if (completedTask != completionTask
                            && !completionTask.IsCompleted)
                        {
                            ownedDispatchLifetime = null;
                            _ = RetireAbandonedCompletionPersistenceAsync(
                                completionTask,
                                dispatchLifetime);
                            completionPersistenceFailureResponse =
                                CreateCompletionPersistenceTimeoutResponse(request);
                        }
                        else
                        {
                            completionResult = await completionTask.ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (
                        persistenceCutoffToken.IsCancellationRequested)
                    {
                        completionPersistenceFailureResponse =
                            CreateCompletionPersistenceTimeoutResponse(request);
                    }

                    if (completionPersistenceFailureResponse == null
                        && !completionResult.IsSuccess)
                    {
                        completionPersistenceFailureResponse = UnityIpcResponseFactory.CreateErrorResponse(
                            request,
                            UcliCoreErrorCodes.InternalError,
                            $"Recoverable IPC operation completion could not be persisted. {completionResult.ErrorMessage}",
                            null);
                    }
                }

                if (cancellation.Reason == IpcRequestCancellationReason.Upstream)
                {
                    throw new OperationCanceledException(cancellation.Token);
                }

                if (completionPersistenceFailureResponse != null)
                {
                    return completionPersistenceFailureResponse;
                }

                if (cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline
                    && !IsExecutionDeadlineResponse(response))
                {
                    throw new OperationCanceledException(cancellation.Token);
                }

                return response;
            }
            finally
            {
                ownedDispatchLifetime?.Retire();
            }
        }

        private static IpcResponse CreateCompletionPersistenceTimeoutResponse (ValidatedUnityIpcRequest request)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InternalError,
                "Recoverable IPC operation completion did not finish before the request persistence cutoff.",
                null);
        }

        private static Task<RecoverableIpcOperationStoreResult> StartCompletionPersistence (
            RecoverableIpcOperationContext context,
            IpcResponse response,
            CancellationToken cancellationToken)
        {
            try
            {
                return context.MarkCompletedAsync(response, cancellationToken).AsTask();
            }
            catch (Exception exception)
            {
                // Terminal publication owns both synchronous startup failures and asynchronous
                // persistence failures so the dispatcher can classify them at one boundary.
                return Task.FromException<RecoverableIpcOperationStoreResult>(exception);
            }
        }

        private static async Task RetireAbandonedRecoverableDispatchAsync (
            Task<RecoverableTerminalResponse> terminalResponseTask,
            RecoverableIpcDispatchLifetime dispatchLifetime)
        {
            try
            {
                var terminalResponse = await terminalResponseTask.ConfigureAwait(false);
                if (terminalResponse.CompletionPersistenceTask != null)
                {
                    await terminalResponse.CompletionPersistenceTask.ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // The lane outcome has already escaped dispatch. Actual work retirement,
                // rather than its outcome, owns the phase resources and retry fence.
            }
            finally
            {
                dispatchLifetime.Retire();
            }
        }

        private static async Task RetireAbandonedCompletionPersistenceAsync (
            Task<RecoverableIpcOperationStoreResult> completionTask,
            RecoverableIpcDispatchLifetime dispatchLifetime)
        {
            try
            {
                await completionTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The dispatcher has already returned the persistence-cutoff response.
                // The retry fence remains owned until the started write actually retires.
            }
            finally
            {
                dispatchLifetime.Retire();
            }
        }

        private Task<RecoverableTerminalResponse> ExecuteRecoverableHandlerOnSelectedLaneAsync (
            IRecoverableUnityIpcMethodHandler methodHandler,
            ValidatedUnityIpcRequest request,
            RecoverableIpcOperationContext context,
            TaskCompletionSource<RecoverableTerminalResponse> terminalResponseSource,
            IpcRequestCancellation cancellation,
            CancellationToken persistenceCutoffToken,
            RecoverableIpcDispatchLifetime dispatchLifetime)
        {
            var reportMaintenanceFailure = methodHandler is not IUnityControlPlaneIpcMethodHandler;
            Func<Task<RecoverableTerminalResponse>> workItem = async () =>
            {
                if (!dispatchLifetime.TryStartWork())
                {
                    throw new OperationCanceledException(
                        "Recoverable IPC work admission ended before the lane started the work item.");
                }

                try
                {
                    if (reportMaintenanceFailure)
                    {
                        ReportMaintenanceFailureOnMainThread();
                    }

                    var response = EnsureCorrelatedResponse(
                        request,
                        await methodHandler.HandleRecoverableAsync(
                            request,
                            context,
                            cancellation)
                            .ConfigureAwait(false));
                    Task<RecoverableIpcOperationStoreResult>? completionPersistenceTask = null;
                    if (context.HasOperationRecord)
                    {
                        completionPersistenceTask = StartCompletionPersistence(
                            context,
                            response,
                            persistenceCutoffToken);
                    }

                    var terminalResponse = new RecoverableTerminalResponse(
                        response,
                        completionPersistenceTask);
                    terminalResponseSource.TrySetResult(terminalResponse);
                    return terminalResponse;
                }
                catch (Exception exception)
                {
                    terminalResponseSource.TrySetException(exception);
                    throw;
                }
            };

            if (methodHandler is IUnityControlPlaneIpcMethodHandler)
            {
                return controlPlaneRequestExecutor.ExecuteAsync(workItem, cancellation.Token);
            }

            return mutationRequestExecutor.ExecuteAsync(workItem, cancellation.Token);
        }

        private sealed record RecoverableTerminalResponse (
            IpcResponse Response,
            Task<RecoverableIpcOperationStoreResult>? CompletionPersistenceTask);

        private sealed class RecoverableIpcDispatchLifetime
        {
            private const int WorkPending = 0;
            private const int WorkStarted = 1;
            private const int Retired = 2;

            private readonly SemaphoreSlim dispatchGate;

            private readonly TaskCompletionSource<bool> retirementSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            private int state;

            public RecoverableIpcDispatchLifetime (SemaphoreSlim dispatchGate)
            {
                this.dispatchGate = dispatchGate ?? throw new ArgumentNullException(nameof(dispatchGate));
            }

            public Task Retirement => retirementSource.Task;

            public bool TryStartWork ()
            {
                return Interlocked.CompareExchange(
                    ref state,
                    WorkStarted,
                    WorkPending) == WorkPending;
            }

            public bool TryRevokeUnstartedWork ()
            {
                if (Interlocked.CompareExchange(
                        ref state,
                        Retired,
                        WorkPending) != WorkPending)
                {
                    return false;
                }

                CompleteRetirement();
                return true;
            }

            public void Retire ()
            {
                if (Interlocked.Exchange(ref state, Retired) == Retired)
                {
                    return;
                }

                CompleteRetirement();
            }

            private void CompleteRetirement ()
            {
                retirementSource.TrySetResult(true);
                dispatchGate.Release();
            }
        }

        private void ReportMaintenanceFailureOnMainThread ()
        {
            try
            {
                var maintenanceFailure = recoverableOperationStore.ConsumeMaintenanceFailure();
                if (!string.IsNullOrWhiteSpace(maintenanceFailure))
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Ipc,
                        $"Recoverable IPC operation maintenance failed. {maintenanceFailure}");
                }
            }
            catch (Exception)
            {
                // Best-effort maintenance reporting must not change the request outcome.
            }
        }

        /// <summary> Creates one immutable method-handler map keyed by validated Unity IPC method. </summary>
        /// <param name="methodHandlers"> Registered method handlers resolved by DI. </param>
        /// <returns> Method-handler map keyed by validated Unity IPC method. </returns>
        /// <exception cref="ArgumentException"> Thrown when handlers are empty, null, duplicated, or expose undefined methods. </exception>
        private static IReadOnlyDictionary<UnityIpcMethod, IUnityIpcMethodHandler> CreateMethodHandlers (
            IEnumerable<IUnityIpcMethodHandler> methodHandlers)
        {
            var map = new Dictionary<UnityIpcMethod, IUnityIpcMethodHandler>();
            var i = 0;
            foreach (var methodHandler in methodHandlers)
            {
                if (methodHandler == null)
                {
                    throw new ArgumentException($"methodHandlers[{i}] must not be null.", nameof(methodHandlers));
                }

                if (!TextVocabulary.IsDefined(methodHandler.Method))
                {
                    throw new ArgumentException($"methodHandlers[{i}] returned an undefined Unity IPC method.", nameof(methodHandlers));
                }

                if (!map.TryAdd(methodHandler.Method, methodHandler))
                {
                    throw new ArgumentException($"Duplicate IPC method handler is registered: {methodHandler.Method}.", nameof(methodHandlers));
                }

                i++;
            }

            if (map.Count == 0)
            {
                throw new ArgumentException("methodHandlers must contain at least one handler.", nameof(methodHandlers));
            }

            return map;
        }
    }
}
