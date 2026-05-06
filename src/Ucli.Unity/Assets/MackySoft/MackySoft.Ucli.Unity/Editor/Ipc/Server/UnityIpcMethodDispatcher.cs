using System;
using MackySoft.Ucli.Contracts;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements method-based dispatch for authorized Unity IPC requests. </summary>
    internal sealed class UnityIpcMethodDispatcher : IUnityIpcMethodDispatcher
    {
        private readonly IReadOnlyDictionary<string, IUnityIpcMethodHandler> methodHandlers;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcMethodDispatcher" /> class. </summary>
        /// <param name="methodHandlers"> Registered method handlers resolved by DI. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="methodHandlers" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when method handlers are empty or invalid. </exception>
        public UnityIpcMethodDispatcher (IEnumerable<IUnityIpcMethodHandler> methodHandlers)
        {
            if (methodHandlers == null)
            {
                throw new ArgumentNullException(nameof(methodHandlers));
            }

            this.methodHandlers = CreateMethodHandlers(methodHandlers);
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
                if (!methodHandlers.TryGetValue(request.Method, out var methodHandler))
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcProtocolErrorCodes.IpcMethodNotSupported,
                        $"IPC method is not supported: {request.Method}.",
                        null);
                }

                return await methodHandler.Handle(request, cancellationToken);
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