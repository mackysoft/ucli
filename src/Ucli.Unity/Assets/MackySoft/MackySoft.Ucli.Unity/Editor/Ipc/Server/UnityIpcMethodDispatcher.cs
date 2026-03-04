using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements method-based dispatch for authorized Unity IPC requests. </summary>
    internal sealed class UnityIpcMethodDispatcher : IUnityIpcMethodDispatcher
    {
        private readonly IReadOnlyDictionary<string, IMethodHandler> methodHandlers;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcMethodDispatcher" /> class. </summary>
        /// <param name="executeRequestDispatcher"> The execute-request dispatcher dependency. </param>
        /// <param name="testRunService"> The test-run service dependency. </param>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
        public UnityIpcMethodDispatcher (
            IExecuteRequestDispatcher executeRequestDispatcher,
            IUnityTestRunService testRunService,
            IServerVersionProvider serverVersionProvider)
        {
            ArgumentNullException.ThrowIfNull(executeRequestDispatcher);
            ArgumentNullException.ThrowIfNull(testRunService);
            ArgumentNullException.ThrowIfNull(serverVersionProvider);

            methodHandlers = CreateMethodHandlers(executeRequestDispatcher, testRunService, serverVersionProvider);
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
                        IpcErrorCodes.IpcMethodNotSupported,
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
                    IpcErrorCodes.InternalError,
                    $"Unexpected error occurred while handling IPC request. {exception.Message}",
                    null);
            }
        }

        /// <summary> Creates handler table keyed by IPC method name. </summary>
        /// <param name="executeRequestDispatcher"> The execute-request dispatcher dependency. </param>
        /// <param name="testRunService"> The test-run service dependency. </param>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <returns> The handler table. </returns>
        private static IReadOnlyDictionary<string, IMethodHandler> CreateMethodHandlers (
            IExecuteRequestDispatcher executeRequestDispatcher,
            IUnityTestRunService testRunService,
            IServerVersionProvider serverVersionProvider)
        {
            var handlers = new Dictionary<string, IMethodHandler>(StringComparer.Ordinal)
            {
                [IpcMethodNames.Ping] = new PingMethodHandler(serverVersionProvider),
                [IpcMethodNames.Execute] = new ExecuteMethodHandler(executeRequestDispatcher),
                [IpcMethodNames.TestRun] = new TestRunMethodHandler(testRunService),
                [IpcMethodNames.Shutdown] = new ShutdownMethodHandler(),
            };
            return handlers;
        }

        /// <summary> Defines one handler contract for an IPC method. </summary>
        private interface IMethodHandler
        {
            /// <summary> Handles one request for this method. </summary>
            /// <param name="request"> The incoming request envelope. </param>
            /// <param name="cancellationToken"> The cancellation token propagated by dispatch. </param>
            /// <returns> The response envelope. </returns>
            ValueTask<IpcResponse> Handle (
                IpcRequest request,
                CancellationToken cancellationToken);
        }

        /// <summary> Handles <c>ping</c> method requests. </summary>
        private sealed class PingMethodHandler : IMethodHandler
        {
            private readonly IServerVersionProvider serverVersionProvider;

            /// <summary> Initializes a new instance of the <see cref="PingMethodHandler" /> class. </summary>
            /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
            public PingMethodHandler (IServerVersionProvider serverVersionProvider)
            {
                this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            }

            /// <inheritdoc />
            public ValueTask<IpcResponse> Handle (
                IpcRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!UnityIpcRequestCodec.TryDecodePingRequest(
                        request,
                        out IpcPingRequest _,
                        out var errorResponse))
                {
                    return ValueTask.FromResult(errorResponse!);
                }

                var payload = UnityPingResponseCodec.CreatePayload(
                    Application.unityVersion,
                    serverVersionProvider.GetVersion(),
                    EditorApplication.isCompiling);
                return ValueTask.FromResult(UnityIpcResponseFactory.CreateSuccessResponse(request, payload));
            }
        }

        /// <summary> Handles <c>execute</c> method requests. </summary>
        private sealed class ExecuteMethodHandler : IMethodHandler
        {
            private readonly IExecuteRequestDispatcher executeRequestDispatcher;

            /// <summary> Initializes a new instance of the <see cref="ExecuteMethodHandler" /> class. </summary>
            /// <param name="executeRequestDispatcher"> The execute-request dispatcher dependency. </param>
            public ExecuteMethodHandler (IExecuteRequestDispatcher executeRequestDispatcher)
            {
                this.executeRequestDispatcher = executeRequestDispatcher ?? throw new ArgumentNullException(nameof(executeRequestDispatcher));
            }

            /// <inheritdoc />
            public async ValueTask<IpcResponse> Handle (
                IpcRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!UnityIpcRequestCodec.TryDecodeExecuteRequest(
                        request,
                        out IpcExecuteRequest? executeRequest,
                        out var errorResponse))
                {
                    return errorResponse!;
                }

                var context = new ExecuteDispatchContext(
                    RequestId: request.RequestId,
                    ProtocolVersion: request.ProtocolVersion);
                return await executeRequestDispatcher.Dispatch(executeRequest!, context, cancellationToken);
            }
        }

        /// <summary> Handles <c>test.run</c> method requests. </summary>
        private sealed class TestRunMethodHandler : IMethodHandler
        {
            private readonly IUnityTestRunService testRunService;

            /// <summary> Initializes a new instance of the <see cref="TestRunMethodHandler" /> class. </summary>
            /// <param name="testRunService"> The test-run service dependency. </param>
            public TestRunMethodHandler (IUnityTestRunService testRunService)
            {
                this.testRunService = testRunService ?? throw new ArgumentNullException(nameof(testRunService));
            }

            /// <inheritdoc />
            public async ValueTask<IpcResponse> Handle (
                IpcRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!UnityIpcRequestCodec.TryDecodeTestRunRequest(
                        request,
                        out IpcTestRunRequest? testRunRequest,
                        out var errorResponse))
                {
                    return errorResponse!;
                }

                try
                {
                    var payload = await testRunService.Execute(testRunRequest!, cancellationToken);
                    return UnityIpcResponseFactory.CreateSuccessResponse(request, payload);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (ArgumentException exception)
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcErrorCodes.InvalidArgument,
                        exception.Message,
                        null);
                }
                catch (Exception exception)
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcErrorCodes.InternalError,
                        $"Unity test run failed. {exception.Message}",
                        null);
                }
            }
        }

        /// <summary> Handles <c>shutdown</c> method requests. </summary>
        private sealed class ShutdownMethodHandler : IMethodHandler
        {
            /// <inheritdoc />
            public ValueTask<IpcResponse> Handle (
                IpcRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!UnityIpcRequestCodec.TryDecodeShutdownRequest(
                        request,
                        out IpcShutdownRequest _,
                        out var errorResponse))
                {
                    return ValueTask.FromResult(errorResponse!);
                }

                var payload = new IpcShutdownResponse(
                    Accepted: true,
                    Message: "Shutdown request accepted.");
                return ValueTask.FromResult(UnityIpcResponseFactory.CreateSuccessResponse(request, payload));
            }
        }
    }
}
