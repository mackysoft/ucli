using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcMethodDispatcherTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenHandlersAreEmpty_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = CreateDispatcher(Array.Empty<IUnityIpcMethodHandler>());
            });

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ParamName, Is.EqualTo("methodHandlers"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenHandlerEntryIsNull_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = CreateDispatcher(
                    new IUnityIpcMethodHandler[]
                    {
                        null!,
                    });
            });

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ParamName, Is.EqualTo("methodHandlers"));
            Assert.That(exception.Message, Does.Contain("must not be null"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenMethodNameIsDuplicated_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = CreateDispatcher(
                    new IUnityIpcMethodHandler[]
                    {
                        new StubMethodHandler(UnityIpcMethod.Ping, static (_, _) =>
                            new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid()))),
                        new StubMethodHandler(UnityIpcMethod.Ping, static (_, _) =>
                            new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid()))),
                    });
            });

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ParamName, Is.EqualTo("methodHandlers"));
            Assert.That(exception.Message, Does.Contain("Duplicate IPC method handler"));
        }

        [TestCase(UnityIpcMethod.Unspecified)]
        [TestCase((UnityIpcMethod)999)]
        [Category("Size.Small")]
        public void Constructor_WhenHandlerMethodIsUndefined_ThrowsArgumentException (UnityIpcMethod method)
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = CreateDispatcher(
                    new IUnityIpcMethodHandler[]
                    {
                        new StubMethodHandler(method, static (_, _) =>
                            new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid()))),
                    });
            });

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ParamName, Is.EqualTo("methodHandlers"));
            Assert.That(exception.Message, Does.Contain("undefined Unity IPC method"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMethodIsMissingOrNotRegistered_ReturnsCorrelatedMethodNotSupportedError () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = CreateDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new StubMethodHandler(UnityIpcMethod.Ping, static (_, _) =>
                        new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid()))),
                });
            var methods = new[] { null, string.Empty, " ", "unknown.method" };
            foreach (var method in methods)
            {
                var requestId = Guid.NewGuid();
                var request = new IpcRequest(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: requestId,
                    sessionToken: "session-token",
                    method: method,
                    payload: IpcPayloadCodec.SerializeToElement(0),
                    responseMode: "single");

                var response = await TestAwaiter.WaitAsync(
                    dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                    "Unsupported IPC method dispatch",
                    AsyncWaitTimeout);

                Assert.That(response.RequestId, Is.EqualTo(requestId), $"method={method ?? "<null>"}");
                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError), $"method={method ?? "<null>"}");
                Assert.That(response.Errors.Count, Is.EqualTo(1), $"method={method ?? "<null>"}");
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(IpcProtocolErrorCodes.IpcMethodNotSupported),
                    $"method={method ?? "<null>"}");
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DispatchStreaming_WhenMethodIsMissingOrNotRegistered_ReturnsCorrelatedMethodNotSupportedError () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = CreateDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new StubMethodHandler(UnityIpcMethod.Ping, static (_, _) =>
                        new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid()))),
                });
            var methods = new[] { null, string.Empty, " ", "unknown.method" };
            foreach (var method in methods)
            {
                var requestId = Guid.NewGuid();
                var request = new IpcRequest(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: requestId,
                    sessionToken: "session-token",
                    method: method,
                    payload: IpcPayloadCodec.SerializeToElement(0),
                    responseMode: "stream");

                var response = await TestAwaiter.WaitAsync(
                    dispatcher.DispatchStreamingAsync(
                        request,
                        new UnexpectedStreamFrameWriter(),
                        CancellationToken.None).AsUniTask(),
                    "Unsupported streaming IPC method dispatch",
                    AsyncWaitTimeout);

                Assert.That(response.RequestId, Is.EqualTo(requestId), $"method={method ?? "<null>"}");
                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError), $"method={method ?? "<null>"}");
                Assert.That(response.Errors.Count, Is.EqualTo(1), $"method={method ?? "<null>"}");
                Assert.That(
                    response.Errors[0].Code,
                    Is.EqualTo(IpcProtocolErrorCodes.IpcMethodNotSupported),
                    $"method={method ?? "<null>"}");
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenHandlerThrows_ReturnsInternalError () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = CreateDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new StubMethodHandler(UnityIpcMethod.Ping, static (_, _) =>
                        throw new InvalidOperationException("dispatcher-test")),
                });
            var request = CreateRequest(Guid.NewGuid(), UnityIpcMethod.Ping, new IpcPingRequest("tests"));

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Throwing IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(response.Errors[0].Message, Does.Contain("dispatcher-test"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenHandlerResponseIsMissingOrMismatched_ReturnsCorrelatedInternalError () => UniTask.ToCoroutine(async () =>
        {
            var requestId = Guid.Parse("b58c17cc-2cff-4c27-a565-73796ee98aa2");
            var invalidResponses = new IpcResponse[]
            {
                null,
                CreateSuccessResponse(Guid.Parse("abca6037-e4f4-47d9-b3f6-7a880c57727f")),
            };

            foreach (var invalidResponse in invalidResponses)
            {
                var handler = new StubMethodHandler(UnityIpcMethod.Ping, (_, _) =>
                    new ValueTask<IpcResponse>(invalidResponse));
                var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler });
                var request = CreateRequest(requestId, UnityIpcMethod.Ping, new IpcPingRequest("tests"));

                var response = await TestAwaiter.WaitAsync(
                    dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                    "Uncorrelated IPC method response",
                    AsyncWaitTimeout);

                Assert.That(response.RequestId, Is.EqualTo(requestId));
                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
                Assert.That(handler.CallCount, Is.EqualTo(1));
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMethodIsRegistered_DelegatesToHandler () => UniTask.ToCoroutine(async () =>
        {
            var handler = new StubMethodHandler(UnityIpcMethod.Ping, static (request, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId));
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler });
            var request = CreateRequest(Guid.NewGuid(), UnityIpcMethod.Ping, new IpcPingRequest("tests"));

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Successful IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenHandlerIsControlPlane_UsesIndependentControlExecutor () => UniTask.ToCoroutine(async () =>
        {
            var mutationExecutor = new RecordingMutationExecutor();
            var controlExecutor = new RecordingControlPlaneExecutor();
            var handler = new StubControlPlaneMethodHandler(
                UnityIpcMethod.Ping,
                static (request, _) => new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                mutationExecutor,
                controlExecutor,
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(
                    CreateRequest(Guid.NewGuid(), UnityIpcMethod.Ping, new IpcPingRequest("tests")),
                    CancellationToken.None).AsUniTask(),
                "Control-plane IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(controlExecutor.CallCount, Is.EqualTo(1));
            Assert.That(mutationExecutor.CallCount, Is.Zero);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMutationQueueOutlivesPayloadDeadline_ReturnsIpcTimeoutWithoutCallingHandler () => UniTask.ToCoroutine(async () =>
        {
            var mutationExecutor = new CancellationOnlyMutationExecutor();
            var handler = new StubMethodHandler(
                UnityIpcMethod.Compile,
                static (request, _) => new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                mutationExecutor,
                new InlineRequestExecutor(),
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.Compile,
                new IpcCompileRequest(Guid.Parse("00000000-0000-0000-0000-000000000612")) { TimeoutMilliseconds = 50 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Mutation queue execution deadline",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
            Assert.That(handler.CallCount, Is.Zero);
            Assert.That(mutationExecutor.CallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableRecordIsCompleted_ReturnsStoredResponseWithoutCallingHandler () => UniTask.ToCoroutine(async () =>
        {
            var requestId = Guid.NewGuid();
            var completedResponse = CreateSuccessResponse(requestId);
            var store = new StubRecoverableIpcOperationStore
            {
                ReadRecord = new RecoverableIpcOperationRecord
                {
                    State = RecoverableIpcOperationState.Completed,
                    Response = completedResponse,
                },
            };
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static (_, _, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid())));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest(requestId, UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Completed recoverable IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response, Is.SameAs(completedResponse));
            Assert.That(handler.RecoverableCallCount, Is.EqualTo(0));
            Assert.That(store.LastReadRequestPayloadHash, Is.EqualTo(handler.RecoverableRequestPayloadHash));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCompletedRecoverableResponseIsMismatched_ReturnsCorrelatedInternalError () => UniTask.ToCoroutine(async () =>
        {
            var requestId = Guid.NewGuid();
            var store = new StubRecoverableIpcOperationStore
            {
                ReadRecord = new RecoverableIpcOperationRecord
                {
                    State = RecoverableIpcOperationState.Completed,
                    Response = CreateSuccessResponse(Guid.NewGuid()),
                },
            };
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static (_, _, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid())));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest(requestId, UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Mismatched completed recoverable response",
                AsyncWaitTimeout);

            Assert.That(response.RequestId, Is.EqualTo(requestId));
            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(handler.RecoverableCallCount, Is.Zero);
            Assert.That(store.CompletedWriteCallCount, Is.Zero);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableRecordPayloadHashDoesNotMatch_ReturnsInternalErrorWithoutCallingHandler () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore
            {
                ExpectedReadRequestPayloadHash = Sha256Digest.Parse(
                    "711b470712791f2f5eac65e93bca9bc4edb38313b6d56870f8731249b08c166c"),
            };
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static (_, _, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid())));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest(Guid.NewGuid(), UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Hash-mismatched recoverable IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(handler.RecoverableCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableStoreExistsAndHandlerDoesNotSupportRecovery_DelegatesNormally () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            var handler = new StubMethodHandler(UnityIpcMethod.PlayEnter, static (request, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId));
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest(Guid.NewGuid(), UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Plain method handler with recoverable store dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(store.ReadCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableHandlerCreatesPendingRecord_WritesCompletedResponse () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static async (request, context, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellationToken);
                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest(Guid.NewGuid(), UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Pending recoverable IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(handler.RecoverableCallCount, Is.EqualTo(1));
            Assert.That(store.PendingWriteCallCount, Is.EqualTo(1));
            Assert.That(store.CompletedWriteCallCount, Is.EqualTo(1));
            Assert.That(store.CompletedResponse, Is.SameAs(response));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableHandlerResponseIsMismatched_PersistsCorrelatedInternalError () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            var requestId = Guid.NewGuid();
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static async (_, context, cancellationToken) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellationToken);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                return CreateSuccessResponse(Guid.NewGuid());
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest(requestId, UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Mismatched recoverable handler response",
                AsyncWaitTimeout);

            Assert.That(response.RequestId, Is.EqualTo(requestId));
            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(store.CompletedWriteCallCount, Is.EqualTo(1));
            Assert.That(store.CompletedResponse, Is.SameAs(response));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableReadIsIncomplete_DoesNotEnterMutationLane () => UniTask.ToCoroutine(async () =>
        {
            var readPermission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new StubRecoverableIpcOperationStore
            {
                ReadPermission = readPermission.Task,
            };
            var mutationExecutor = new RecordingMutationExecutor();
            var handler = new StubRecoverableMethodHandler(
                UnityIpcMethod.PlayEnter,
                static (request, _, _) => new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                mutationExecutor,
                new InlineRequestExecutor(),
                store,
                NoOpDaemonLogger.Instance);

            var dispatchTask = dispatcher.DispatchAsync(
                CreateRequest(Guid.NewGuid(), UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
                CancellationToken.None);
            await TestAwaiter.WaitAsync(
                store.ReadEntered.Task.AsUniTask(),
                "Recoverable read entry",
                AsyncWaitTimeout);

            Assert.That(mutationExecutor.CallCount, Is.Zero);

            readPermission.TrySetResult(true);
            var response = await TestAwaiter.WaitAsync(
                dispatchTask.AsUniTask(),
                "Recoverable read fence dispatch",
                AsyncWaitTimeout);
            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(mutationExecutor.CallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMaintenanceFailed_ReportsOnMutationLaneWithoutFailingRequest () => UniTask.ToCoroutine(async () =>
        {
            var mutationExecutor = new RecordingMutationExecutor();
            var store = new StubRecoverableIpcOperationStore
            {
                MaintenanceFailureMessage = "maintenance failed",
                ExpectedMaintenanceFailureLane = () => mutationExecutor.IsExecuting,
            };
            var handler = new StubRecoverableMethodHandler(
                UnityIpcMethod.PlayEnter,
                static (request, _, _) => new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                mutationExecutor,
                new InlineRequestExecutor(),
                store,
                NoOpDaemonLogger.Instance);

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(
                    CreateRequest(Guid.NewGuid(), UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
                    CancellationToken.None).AsUniTask(),
                "Recoverable maintenance report",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(store.ConsumeMaintenanceFailureCallCount, Is.EqualTo(1));
            Assert.That(store.MaintenanceFailureConsumedOnExpectedLane, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenPendingPersistenceIsIncomplete_DoesNotPerformMutation () => UniTask.ToCoroutine(async () =>
        {
            var pendingWritePermission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new StubRecoverableIpcOperationStore
            {
                PendingWritePermission = pendingWritePermission.Task,
            };
            var mutationPerformed = false;
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, async (request, context, cancellationToken) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellationToken);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                mutationPerformed = true;
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);

            var dispatchTask = dispatcher.DispatchAsync(
                CreateRequest(Guid.NewGuid(), UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
                CancellationToken.None);
            await TestAwaiter.WaitAsync(
                store.PendingWriteEntered.Task.AsUniTask(),
                "Pending persistence entry",
                AsyncWaitTimeout);

            Assert.That(mutationPerformed, Is.False);

            pendingWritePermission.TrySetResult(true);
            var response = await TestAwaiter.WaitAsync(
                dispatchTask.AsUniTask(),
                "Pending persistence fence dispatch",
                AsyncWaitTimeout);
            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(mutationPerformed, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCompletedPersistenceIsIncomplete_DoesNotReturnTerminalResponse () => UniTask.ToCoroutine(async () =>
        {
            var completedWritePermission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new StubRecoverableIpcOperationStore
            {
                CompletedWritePermission = completedWritePermission.Task,
            };
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static async (request, context, cancellationToken) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellationToken);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);

            var dispatchTask = dispatcher.DispatchAsync(
                CreateRequest(Guid.NewGuid(), UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
                CancellationToken.None);
            await TestAwaiter.WaitAsync(
                store.CompletedWriteEntered.Task.AsUniTask(),
                "Completed persistence entry",
                AsyncWaitTimeout);

            Assert.That(dispatchTask.IsCompleted, Is.False);

            completedWritePermission.TrySetResult(true);
            var response = await TestAwaiter.WaitAsync(
                dispatchTask.AsUniTask(),
                "Completed persistence fence dispatch",
                AsyncWaitTimeout);
            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCompletedPersistenceIgnoresCancellation_ReturnsAtHardDeadlineAndObservesLateTask () => UniTask.ToCoroutine(async () =>
        {
            var completedWritePermission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new StubRecoverableIpcOperationStore
            {
                CompletedWritePermission = completedWritePermission.Task,
            };
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static async (request, context, cancellationToken) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellationToken);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(
                    CreateRequest(Guid.NewGuid(), UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 5000 }),
                    CancellationToken.None).AsUniTask(),
                "Completed persistence hard deadline",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(response.Errors[0].Message, Does.Contain("completion persistence exceeded"));
            Assert.That(store.CompletedWriteCallCount, Is.EqualTo(1));

            completedWritePermission.TrySetResult(true);
            await TestAwaiter.WaitAsync(
                store.CompletedWriteExited.Task.AsUniTask(),
                "Late completed persistence observation",
                AsyncWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCallerCancelsAfterTerminalMutation_PersistsCompletedWithIndependentToken () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            using var cancellationTokenSource = new CancellationTokenSource();
            var requestId = Guid.NewGuid();
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, async (request, context, cancellationToken) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellationToken);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                cancellationTokenSource.Cancel();
                return CreateSuccessResponse(request.RequestId);
            });
            var executor = new PostExecutionCancellationExecutor();
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                executor,
                executor,
                store,
                NoOpDaemonLogger.Instance);

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await dispatcher.DispatchAsync(
                    CreateRequest(requestId, UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
                    cancellationTokenSource.Token).AsUniTask();
            }, "Recoverable terminal cancellation", AsyncWaitTimeout);

            Assert.That(store.CompletedWriteCallCount, Is.EqualTo(1));
            Assert.That(store.CompletedWriteObservedCancellation, Is.False);
            Assert.That(store.CompletedResponse, Is.Not.Null);
            Assert.That(store.CompletedResponse.RequestId, Is.EqualTo(requestId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableHandlerReturnsError_PersistsErrorAsCompletedBeforeReturning () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            var requestId = Guid.NewGuid();
            var terminalErrorResponse = new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                status: IpcProtocol.StatusError,
                payload: default,
                errors: new[] { new IpcError(UcliCoreErrorCodes.InternalError, "terminal failure", null) });
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, async (_, context, cancellationToken) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellationToken);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                return terminalErrorResponse;
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(
                    CreateRequest(requestId, UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
                    CancellationToken.None).AsUniTask(),
                "Recoverable terminal error persistence",
                AsyncWaitTimeout);

            Assert.That(response, Is.SameAs(terminalErrorResponse));
            Assert.That(store.CompletedWriteCallCount, Is.EqualTo(1));
            Assert.That(store.CompletedResponse, Is.SameAs(terminalErrorResponse));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableRecordIsInvalid_ReturnsInternalErrorWithoutCallingHandler () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore
            {
                ReadErrorMessage = "invalid record",
            };
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static (_, _, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid())));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest(Guid.NewGuid(), UnityIpcMethod.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Invalid recoverable IPC record dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(handler.RecoverableCallCount, Is.EqualTo(0));
            Assert.That(store.CompletedWriteCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCancellationIsRequested_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var handler = new StubMethodHandler(UnityIpcMethod.Ping, static (_, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid())));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler });
            var request = CreateRequest(Guid.NewGuid(), UnityIpcMethod.Ping, new IpcPingRequest("tests"));
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await dispatcher.DispatchAsync(request, cancellationTokenSource.Token).AsUniTask();
            }, "Canceled IPC method dispatch", AsyncWaitTimeout);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        });

        private static IpcRequest CreateRequest (
            Guid requestId,
            UnityIpcMethod method,
            object payload)
        {
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: "session-token",
                method: ContractLiteralCodec.ToValue(method),
                payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: "single");
        }

        private static UnityIpcMethodDispatcher CreateDispatcher (
            IUnityIpcMethodHandler[] handlers,
            IRecoverableIpcOperationStore recoverableOperationStore = null)
        {
            var executor = new InlineRequestExecutor();
            return new UnityIpcMethodDispatcher(
                handlers,
                executor,
                executor,
                recoverableOperationStore,
                NoOpDaemonLogger.Instance);
        }

        private static IpcResponse CreateSuccessResponse (Guid requestId)
        {
            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                status: IpcProtocol.StatusOk,
                payload: IpcPayloadCodec.SerializeToElement(new { ok = true }),
                errors: Array.Empty<IpcError>());
        }

        private sealed class StubMethodHandler : IUnityIpcMethodHandler
        {
            private readonly Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handle;

            public StubMethodHandler (
                UnityIpcMethod method,
                Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public int CallCount { get; private set; }

            public UnityIpcMethod Method { get; }

            public ValueTask<IpcResponse> HandleAsync (
                IpcRequest request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return handle(request, cancellationToken);
            }
        }

        private sealed class StubControlPlaneMethodHandler : IUnityControlPlaneIpcMethodHandler
        {
            private readonly Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handle;

            public StubControlPlaneMethodHandler (
                UnityIpcMethod method,
                Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public UnityIpcMethod Method { get; }

            public ValueTask<IpcResponse> HandleAsync (
                IpcRequest request,
                CancellationToken cancellationToken)
            {
                return handle(request, cancellationToken);
            }
        }

        private sealed class UnexpectedStreamFrameWriter : IIpcStreamFrameWriter
        {
            public ValueTask WriteProgressAsync<TPayload> (
                string eventName,
                TPayload payload,
                CancellationToken cancellationToken = default)
                where TPayload : notnull
            {
                throw new InvalidOperationException("Unsupported method dispatch must not write a progress frame.");
            }

            public ValueTask WriteTerminalAsync (
                IpcResponse response,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Unsupported method dispatch must not write a terminal frame.");
            }
        }

        private sealed class RecordingMutationExecutor : IUnityMainThreadRequestExecutor
        {
            public int CallCount { get; private set; }

            public bool IsExecuting { get; private set; }

            public async Task<T> ExecuteAsync<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                IsExecuting = true;
                try
                {
                    return await workItem();
                }
                finally
                {
                    IsExecuting = false;
                }
            }
        }

        private sealed class CancellationOnlyMutationExecutor : IUnityMainThreadRequestExecutor
        {
            public int CallCount { get; private set; }

            public async Task<T> ExecuteAsync<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return default;
            }
        }

        private sealed class InlineRequestExecutor :
            IUnityMainThreadRequestExecutor,
            IUnityControlPlaneRequestExecutor
        {
            public Task<T> ExecuteAsync<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return workItem();
            }
        }

        private sealed class PostExecutionCancellationExecutor :
            IUnityMainThreadRequestExecutor,
            IUnityControlPlaneRequestExecutor
        {
            public async Task<T> ExecuteAsync<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await workItem();
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
        }

        private sealed class RecordingControlPlaneExecutor : IUnityControlPlaneRequestExecutor
        {
            public int CallCount { get; private set; }

            public Task<T> ExecuteAsync<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                return workItem();
            }
        }

        private sealed class StubRecoverableMethodHandler : IRecoverableUnityIpcMethodHandler
        {
            private readonly Func<IpcRequest, RecoverableIpcOperationContext, CancellationToken, ValueTask<IpcResponse>> handle;

            public StubRecoverableMethodHandler (
                UnityIpcMethod method,
                Func<IpcRequest, RecoverableIpcOperationContext, CancellationToken, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public int PlainCallCount { get; private set; }

            public int RecoverableCallCount { get; private set; }

            public UnityIpcMethod Method { get; }

            public Sha256Digest RecoverableRequestPayloadHash { get; set; } = Sha256Digest.Parse(
                "cba57ba7022e93f53db3a8a509e17fbf0ccdf297345f12346e3ed4d7c6a852db");

            public bool TryCreateRecoverableRequestPayloadHash (
                IpcRequest request,
                out Sha256Digest requestPayloadHash,
                out IpcResponse errorResponse)
            {
                requestPayloadHash = RecoverableRequestPayloadHash;
                errorResponse = null;
                return true;
            }

            public ValueTask<IpcResponse> HandleAsync (
                IpcRequest request,
                CancellationToken cancellationToken)
            {
                PlainCallCount++;
                return handle(request, null, cancellationToken);
            }

            public ValueTask<IpcResponse> HandleRecoverableAsync (
                IpcRequest request,
                RecoverableIpcOperationContext context,
                CancellationToken cancellationToken)
            {
                RecoverableCallCount++;
                return handle(request, context, cancellationToken);
            }
        }

        private sealed class StubRecoverableIpcOperationStore : IRecoverableIpcOperationStore
        {
            public RecoverableIpcOperationRecord ReadRecord { get; set; }

            public string ReadErrorMessage { get; set; }

            public Sha256Digest ExpectedReadRequestPayloadHash { get; set; }

            public Sha256Digest LastReadRequestPayloadHash { get; private set; }

            public int ReadCallCount { get; private set; }

            public int PendingWriteCallCount { get; private set; }

            public int CompletedWriteCallCount { get; private set; }

            public IpcResponse CompletedResponse { get; private set; }

            public bool CompletedWriteObservedCancellation { get; private set; }

            public string MaintenanceFailureMessage { get; set; }

            public Func<bool> ExpectedMaintenanceFailureLane { get; set; }

            public int ConsumeMaintenanceFailureCallCount { get; private set; }

            public bool MaintenanceFailureConsumedOnExpectedLane { get; private set; }

            public Task ReadPermission { get; set; }

            public Task PendingWritePermission { get; set; }

            public Task CompletedWritePermission { get; set; }

            public TaskCompletionSource<bool> ReadEntered { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<bool> PendingWriteEntered { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<bool> CompletedWriteEntered { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<bool> CompletedWriteExited { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public async ValueTask<RecoverableIpcOperationReadResult> ReadAsync (
                UnityIpcMethod method,
                Guid requestId,
                Sha256Digest requestPayloadHash,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadCallCount++;
                LastReadRequestPayloadHash = requestPayloadHash;
                ReadEntered.TrySetResult(true);
                if (ReadPermission != null)
                {
                    await ReadPermission;
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (ExpectedReadRequestPayloadHash != null
                    && ExpectedReadRequestPayloadHash != requestPayloadHash)
                {
                    return RecoverableIpcOperationReadResult.Failure("identity mismatch");
                }

                return !string.IsNullOrWhiteSpace(ReadErrorMessage)
                    ? RecoverableIpcOperationReadResult.Failure(ReadErrorMessage)
                    : ReadRecord != null
                        ? RecoverableIpcOperationReadResult.Success(ReadRecord)
                        : RecoverableIpcOperationReadResult.Missing();
            }

            public async ValueTask<RecoverableIpcOperationStoreResult> WritePendingAsync (
                UnityIpcMethod method,
                Guid requestId,
                Sha256Digest requestPayloadHash,
                DateTimeOffset startedAtUtc,
                System.Text.Json.JsonElement recoveryPayload,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PendingWriteCallCount++;
                PendingWriteEntered.TrySetResult(true);
                if (PendingWritePermission != null)
                {
                    await PendingWritePermission;
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return RecoverableIpcOperationStoreResult.Success();
            }

            public async ValueTask<RecoverableIpcOperationStoreResult> WriteCompletedAsync (
                UnityIpcMethod method,
                Guid requestId,
                Sha256Digest requestPayloadHash,
                DateTimeOffset startedAtUtc,
                DateTimeOffset completedAtUtc,
                System.Text.Json.JsonElement recoveryPayload,
                IpcResponse response,
                CancellationToken cancellationToken)
            {
                CompletedWriteObservedCancellation = cancellationToken.IsCancellationRequested;
                cancellationToken.ThrowIfCancellationRequested();
                CompletedWriteCallCount++;
                CompletedResponse = response;
                CompletedWriteEntered.TrySetResult(true);
                try
                {
                    if (CompletedWritePermission != null)
                    {
                        await CompletedWritePermission;
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    return RecoverableIpcOperationStoreResult.Success();
                }
                finally
                {
                    CompletedWriteExited.TrySetResult(true);
                }
            }

            public string ConsumeMaintenanceFailure ()
            {
                ConsumeMaintenanceFailureCallCount++;
                MaintenanceFailureConsumedOnExpectedLane = ExpectedMaintenanceFailureLane?.Invoke() ?? true;
                var message = MaintenanceFailureMessage;
                MaintenanceFailureMessage = null;
                return message;
            }
        }
    }
}
