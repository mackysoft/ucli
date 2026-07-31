using System;
using System.Collections.Generic;
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

        /// <summary> Initializes a new instance of the <see cref="UnityIpcMethodDispatcher" /> class. </summary>
        /// <param name="methodHandlers"> Registered method handlers resolved by DI. </param>
        /// <param name="mutationRequestExecutor"> The serialized executor for Unity mutation requests. </param>
        /// <param name="controlPlaneRequestExecutor"> The independent executor for control-plane requests. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="methodHandlers" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when method handlers are empty or invalid. </exception>
        public UnityIpcMethodDispatcher (
            IEnumerable<IUnityIpcMethodHandler> methodHandlers,
            IUnityMainThreadRequestExecutor mutationRequestExecutor,
            IUnityControlPlaneRequestExecutor controlPlaneRequestExecutor)
        {
            if (methodHandlers == null)
            {
                throw new ArgumentNullException(nameof(methodHandlers));
            }

            this.methodHandlers = CreateMethodHandlers(methodHandlers);
            this.mutationRequestExecutor = mutationRequestExecutor ?? throw new ArgumentNullException(nameof(mutationRequestExecutor));
            this.controlPlaneRequestExecutor = controlPlaneRequestExecutor ?? throw new ArgumentNullException(nameof(controlPlaneRequestExecutor));
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
        /// <param name="response"> The response produced by a method handler. </param>
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
