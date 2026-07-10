using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements method-based dispatch for authorized Unity IPC requests. </summary>
    internal sealed class UnityIpcMethodDispatcher : IUnityIpcMethodDispatcher
    {
        private static readonly TimeSpan CompletedPersistenceFinalizationTimeout = TimeSpan.FromSeconds(1);

        private readonly IReadOnlyDictionary<string, IUnityIpcMethodHandler> methodHandlers;

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

        /// <summary> Dispatches one IPC request envelope by method contract. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The response envelope for the request. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public async Task<IpcResponse> DispatchAsync (
            IpcRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var executionDeadlineScope = CreateExecutionDeadlineScope(request, cancellationToken);
            var executionCancellationToken = executionDeadlineScope?.Token ?? cancellationToken;

            try
            {
                if (!methodHandlers.TryGetValue(request.Method, out var methodHandler))
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcProtocolErrorCodes.IpcMethodNotSupported,
                        $"IPC method is not supported: {request.Method}.",
                        null);
                }

                return await ExecuteOnSelectedLaneAsync(methodHandler, request, executionCancellationToken);
            }
            catch (OperationCanceledException) when (IsExecutionDeadlineCancellation(executionDeadlineScope, cancellationToken))
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
            IpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (streamWriter == null)
            {
                throw new ArgumentNullException(nameof(streamWriter));
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var executionDeadlineScope = CreateExecutionDeadlineScope(request, cancellationToken);
            var executionCancellationToken = executionDeadlineScope?.Token ?? cancellationToken;

            try
            {
                if (!methodHandlers.TryGetValue(request.Method, out var methodHandler))
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcProtocolErrorCodes.IpcMethodNotSupported,
                        $"IPC method is not supported: {request.Method}.",
                        null);
                }

                if (methodHandler is not IStreamingUnityIpcMethodHandler streamingMethodHandler)
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcProtocolErrorCodes.IpcMethodNotSupported,
                        $"IPC method does not support streaming: {request.Method}.",
                        null);
                }

                return await ExecuteOnSelectedLaneAsync(streamingMethodHandler, request, streamWriter, executionCancellationToken);
            }
            catch (OperationCanceledException) when (IsExecutionDeadlineCancellation(executionDeadlineScope, cancellationToken))
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
            catch (Exception exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Unexpected error occurred while handling streaming IPC request. {exception.Message}",
                    null);
            }
        }

        private static CancellationTokenSource? CreateExecutionDeadlineScope (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            if (request.Payload.ValueKind != System.Text.Json.JsonValueKind.Object
                || !request.Payload.TryGetProperty("timeoutMilliseconds", out var timeoutElement)
                || timeoutElement.ValueKind != System.Text.Json.JsonValueKind.Number
                || !timeoutElement.TryGetInt32(out var timeoutMilliseconds)
                || timeoutMilliseconds <= 0)
            {
                return null;
            }

            var scope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            scope.CancelAfter(timeoutMilliseconds);
            return scope;
        }

        private static bool IsExecutionDeadlineCancellation (
            CancellationTokenSource? executionDeadlineScope,
            CancellationToken callerCancellationToken)
        {
            return executionDeadlineScope != null
                && executionDeadlineScope.IsCancellationRequested
                && !callerCancellationToken.IsCancellationRequested;
        }

        private static IpcResponse CreateExecutionTimeoutResponse (IpcRequest request)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                IpcTransportErrorCodes.IpcTimeout,
                $"Unity IPC request timed out before method execution reached a terminal state: {request.Method}.",
                null);
        }

        private Task<IpcResponse> ExecuteOnSelectedLaneAsync (
            IUnityIpcMethodHandler methodHandler,
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            if (methodHandler is IRecoverableUnityIpcMethodHandler recoverableMethodHandler
                && recoverableOperationStore != null)
            {
                return DispatchRecoverableAsync(recoverableMethodHandler, request, cancellationToken);
            }

            Func<Task<IpcResponse>> workItem = () => methodHandler
                .HandleAsync(request, cancellationToken)
                .AsTask();

            if (methodHandler is IUnityControlPlaneIpcMethodHandler)
            {
                return controlPlaneRequestExecutor.ExecuteAsync(workItem, cancellationToken);
            }

            return mutationRequestExecutor.ExecuteAsync(workItem, cancellationToken);
        }

        private Task<IpcResponse> ExecuteOnSelectedLaneAsync (
            IStreamingUnityIpcMethodHandler methodHandler,
            IpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken)
        {
            Func<Task<IpcResponse>> workItem = () => methodHandler
                .HandleStreamingAsync(request, streamWriter, cancellationToken)
                .AsTask();
            if (methodHandler is IUnityControlPlaneIpcMethodHandler)
            {
                return controlPlaneRequestExecutor.ExecuteAsync(workItem, cancellationToken);
            }

            return mutationRequestExecutor.ExecuteAsync(workItem, cancellationToken);
        }

        private async Task<IpcResponse> DispatchRecoverableAsync (
            IRecoverableUnityIpcMethodHandler methodHandler,
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            if (!methodHandler.TryCreateRecoverableRequestPayloadHash(
                    request,
                    out var requestPayloadHash,
                    out var hashErrorResponse))
            {
                return hashErrorResponse;
            }

            if (string.IsNullOrWhiteSpace(requestPayloadHash))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"IPC method '{request.Method}' returned an empty recoverable request payload hash.",
                    null);
            }

            await recoverableDispatchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var readResult = await recoverableOperationStore.ReadAsync(
                        request.Method,
                        request.RequestId,
                        requestPayloadHash,
                        cancellationToken)
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
                    request.Method,
                    request.RequestId,
                    requestPayloadHash,
                    record);
                var terminalResponseSource = new TaskCompletionSource<IpcResponse>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var laneExecutionTask = ExecuteRecoverableHandlerOnSelectedLaneAsync(
                    methodHandler,
                    request,
                    context,
                    terminalResponseSource,
                    cancellationToken);

                var executionWasCanceledAfterTerminalResponse = false;
                IpcResponse response;
                try
                {
                    response = await laneExecutionTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    terminalResponseSource.Task.Status == TaskStatus.RanToCompletion)
                {
                    response = await terminalResponseSource.Task.ConfigureAwait(false);
                    executionWasCanceledAfterTerminalResponse = true;
                }

                IpcResponse completionPersistenceFailureResponse = null;
                if (context.HasOperationRecord)
                {
                    var finalizationCancellationTokenSource = new CancellationTokenSource(
                        CompletedPersistenceFinalizationTimeout);
                    RecoverableIpcOperationStoreResult completionResult = null;
                    try
                    {
                        var completionTask = context.MarkCompletedAsync(
                                response,
                                finalizationCancellationTokenSource.Token)
                            .AsTask();
                        var hardDeadlineTask = Task.Delay(CompletedPersistenceFinalizationTimeout);
                        var completedTask = await Task.WhenAny(completionTask, hardDeadlineTask).ConfigureAwait(false);
                        if (completedTask != completionTask && !completionTask.IsCompleted)
                        {
                            ObserveLateCompletionPersistence(
                                completionTask,
                                finalizationCancellationTokenSource);
                            finalizationCancellationTokenSource = null;
                            completionPersistenceFailureResponse = CreateCompletionPersistenceTimeoutResponse(request);
                        }
                        else
                        {
                            completionResult = await completionTask.ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (
                        finalizationCancellationTokenSource?.IsCancellationRequested == true)
                    {
                        completionPersistenceFailureResponse = CreateCompletionPersistenceTimeoutResponse(request);
                    }
                    finally
                    {
                        finalizationCancellationTokenSource?.Dispose();
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

                if (executionWasCanceledAfterTerminalResponse || cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return completionPersistenceFailureResponse ?? response;
            }
            finally
            {
                recoverableDispatchGate.Release();
            }
        }

        private static IpcResponse CreateCompletionPersistenceTimeoutResponse (IpcRequest request)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InternalError,
                $"Recoverable IPC operation completion persistence exceeded {CompletedPersistenceFinalizationTimeout.TotalMilliseconds:0} milliseconds.",
                null);
        }

        private static void ObserveLateCompletionPersistence (
            Task<RecoverableIpcOperationStoreResult> completionTask,
            CancellationTokenSource cancellationTokenSource)
        {
            _ = ObserveLateCompletionPersistenceAsync(completionTask, cancellationTokenSource);
        }

        private static async Task ObserveLateCompletionPersistenceAsync (
            Task<RecoverableIpcOperationStoreResult> completionTask,
            CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                await completionTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The request already returned a persistence failure. Observe late faults
                // without invoking Unity APIs or logging from this background continuation.
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }

        private Task<IpcResponse> ExecuteRecoverableHandlerOnSelectedLaneAsync (
            IRecoverableUnityIpcMethodHandler methodHandler,
            IpcRequest request,
            RecoverableIpcOperationContext context,
            TaskCompletionSource<IpcResponse> terminalResponseSource,
            CancellationToken cancellationToken)
        {
            var reportMaintenanceFailure = methodHandler is not IUnityControlPlaneIpcMethodHandler;
            Func<Task<IpcResponse>> workItem = async () =>
            {
                if (reportMaintenanceFailure)
                {
                    ReportMaintenanceFailureOnMainThread();
                }

                var response = await methodHandler.HandleRecoverableAsync(
                    request,
                    context,
                    cancellationToken);
                terminalResponseSource.TrySetResult(response);
                return response;
            };

            if (methodHandler is IUnityControlPlaneIpcMethodHandler)
            {
                return controlPlaneRequestExecutor.ExecuteAsync(workItem, cancellationToken);
            }

            return mutationRequestExecutor.ExecuteAsync(workItem, cancellationToken);
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

        /// <summary> Creates one immutable method-handler map keyed by IPC method name. </summary>
        /// <param name="methodHandlers"> Registered method handlers resolved by DI. </param>
        /// <returns> Method-handler map keyed by method name. </returns>
        /// <exception cref="ArgumentException"> Thrown when handlers are empty, null, duplicated, or have invalid method names. </exception>
        private static IReadOnlyDictionary<string, IUnityIpcMethodHandler> CreateMethodHandlers (
            IEnumerable<IUnityIpcMethodHandler> methodHandlers)
        {
            var map = new Dictionary<string, IUnityIpcMethodHandler>(StringComparer.Ordinal);
            var i = 0;
            foreach (var methodHandler in methodHandlers)
            {
                if (methodHandler == null)
                {
                    throw new ArgumentException($"methodHandlers[{i}] must not be null.", nameof(methodHandlers));
                }

                if (string.IsNullOrWhiteSpace(methodHandler.Method))
                {
                    throw new ArgumentException($"methodHandlers[{i}] returned an empty method name.", nameof(methodHandlers));
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
