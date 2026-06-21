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
        private readonly IReadOnlyDictionary<string, IUnityIpcMethodHandler> methodHandlers;

        private readonly IRecoverableIpcOperationStore recoverableOperationStore;

        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcMethodDispatcher" /> class. </summary>
        /// <param name="methodHandlers"> Registered method handlers resolved by DI. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="methodHandlers" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when method handlers are empty or invalid. </exception>
        public UnityIpcMethodDispatcher (
            IEnumerable<IUnityIpcMethodHandler> methodHandlers,
            IRecoverableIpcOperationStore recoverableOperationStore = null,
            IDaemonLogger daemonLogger = null)
        {
            if (methodHandlers == null)
            {
                throw new ArgumentNullException(nameof(methodHandlers));
            }

            this.methodHandlers = CreateMethodHandlers(methodHandlers);
            this.recoverableOperationStore = recoverableOperationStore;
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
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

                if (methodHandler is IRecoverableUnityIpcMethodHandler recoverableMethodHandler
                    && recoverableOperationStore != null)
                {
                    using (RuntimePerformanceTracer.Measure(RuntimePerformanceTracer.SectionNames.Dispatch))
                    {
                        return await DispatchRecoverableAsync(
                            recoverableMethodHandler,
                            request,
                            cancellationToken);
                    }
                }

                using (RuntimePerformanceTracer.Measure(RuntimePerformanceTracer.SectionNames.Dispatch))
                {
                    return await methodHandler.HandleAsync(request, cancellationToken);
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

                using (RuntimePerformanceTracer.Measure(RuntimePerformanceTracer.SectionNames.Dispatch))
                {
                    return await streamingMethodHandler.HandleStreamingAsync(request, streamWriter, cancellationToken);
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
                    UcliCoreErrorCodes.InternalError,
                    $"Unexpected error occurred while handling streaming IPC request. {exception.Message}",
                    null);
            }
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

            if (!recoverableOperationStore.TryPurgeExpiredRecords(DateTimeOffset.UtcNow, out var purgeErrorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"Recoverable IPC operation purge failed. {purgeErrorMessage}");
            }

            var hasRecord = recoverableOperationStore.TryRead(
                request.Method,
                request.RequestId,
                requestPayloadHash,
                out var record,
                out var readErrorMessage);
            if (!string.IsNullOrWhiteSpace(readErrorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"Recoverable IPC operation read failed. {readErrorMessage}");
                if (!hasRecord)
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        UcliCoreErrorCodes.InternalError,
                        $"Recoverable IPC operation state could not be read. {readErrorMessage}",
                        null);
                }
            }

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
            var response = await methodHandler.HandleRecoverableAsync(request, context, cancellationToken);
            if (context.HasOperationRecord
                && !context.TryMarkCompleted(response, out var completeErrorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    $"Recoverable IPC operation completion write failed. {completeErrorMessage}");
            }

            return response;
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
