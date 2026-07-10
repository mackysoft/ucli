using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
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
                        new StubMethodHandler(IpcMethodNames.Ping, static (_, _) =>
                            new ValueTask<IpcResponse>(CreateSuccessResponse("req-1"))),
                        new StubMethodHandler(IpcMethodNames.Ping, static (_, _) =>
                            new ValueTask<IpcResponse>(CreateSuccessResponse("req-1"))),
                    });
            });

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ParamName, Is.EqualTo("methodHandlers"));
            Assert.That(exception.Message, Does.Contain("Duplicate IPC method handler"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMethodIsNotRegistered_ReturnsMethodNotSupportedError () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = CreateDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new StubMethodHandler(IpcMethodNames.Ping, static (_, _) =>
                        new ValueTask<IpcResponse>(CreateSuccessResponse("req-unsupported"))),
                });
            var request = CreateRequest("req-unsupported", method: "unknown.method", payload: 0);

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Unsupported IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcProtocolErrorCodes.IpcMethodNotSupported));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenHandlerThrows_ReturnsInternalError () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = CreateDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new StubMethodHandler(IpcMethodNames.Ping, static (_, _) =>
                        throw new InvalidOperationException("dispatcher-test")),
                });
            var request = CreateRequest("req-throw", IpcMethodNames.Ping, new IpcPingRequest("tests"));

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
        public IEnumerator Dispatch_WhenMethodIsRegistered_DelegatesToHandler () => UniTask.ToCoroutine(async () =>
        {
            var handler = new StubMethodHandler(IpcMethodNames.Ping, static (request, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId));
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler });
            var request = CreateRequest("req-ok", IpcMethodNames.Ping, new IpcPingRequest("tests"));

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Successful IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(response.RequestId, Is.EqualTo("req-ok"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenHandlerIsControlPlane_UsesIndependentControlExecutor () => UniTask.ToCoroutine(async () =>
        {
            var mutationExecutor = new RecordingMutationExecutor();
            var controlExecutor = new RecordingControlPlaneExecutor();
            var handler = new StubControlPlaneMethodHandler(
                IpcMethodNames.Ping,
                static (request, _) => new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                mutationExecutor,
                controlExecutor,
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(
                    CreateRequest("req-control-lane", IpcMethodNames.Ping, new IpcPingRequest("tests")),
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
                IpcMethodNames.Compile,
                static (request, _) => new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                mutationExecutor,
                new InlineRequestExecutor(),
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);
            var request = CreateRequest(
                "req-queue-timeout",
                IpcMethodNames.Compile,
                new IpcCompileRequest("run-id") { TimeoutMilliseconds = 50 });

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
            var completedResponse = CreateSuccessResponse("req-recovered-completed");
            var store = new StubRecoverableIpcOperationStore
            {
                ReadRecord = new RecoverableIpcOperationRecord
                {
                    State = RecoverableIpcOperationState.Completed,
                    Response = completedResponse,
                },
            };
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, static (_, _, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse("unexpected")));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest("req-recovered-completed", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

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
        public IEnumerator Dispatch_WhenRecoverableRecordPayloadHashDoesNotMatch_ReturnsInternalErrorWithoutCallingHandler () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore
            {
                ExpectedReadRequestPayloadHash = "other-hash",
            };
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, static (_, _, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse("unexpected")));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest("req-recovered-hash-mismatch", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

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
            var handler = new StubMethodHandler(IpcMethodNames.PlayEnter, static (request, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId));
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest("req-plain-handler-with-store", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Plain method handler with recoverable store dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.RequestId, Is.EqualTo("req-plain-handler-with-store"));
            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(store.ReadCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableHandlerCreatesPendingRecord_WritesCompletedResponse () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, static async (request, context, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellationToken);
                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest("req-recovered-pending", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

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
        public IEnumerator Dispatch_WhenRecoverableReadIsIncomplete_DoesNotEnterMutationLane () => UniTask.ToCoroutine(async () =>
        {
            var readPermission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new StubRecoverableIpcOperationStore
            {
                ReadPermission = readPermission.Task,
            };
            var mutationExecutor = new RecordingMutationExecutor();
            var handler = new StubRecoverableMethodHandler(
                IpcMethodNames.PlayEnter,
                static (request, _, _) => new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                mutationExecutor,
                new InlineRequestExecutor(),
                store,
                NoOpDaemonLogger.Instance);

            var dispatchTask = dispatcher.DispatchAsync(
                CreateRequest("req-read-fence", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
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
                IpcMethodNames.PlayEnter,
                static (request, _, _) => new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                mutationExecutor,
                new InlineRequestExecutor(),
                store,
                NoOpDaemonLogger.Instance);

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(
                    CreateRequest("req-maintenance-report", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
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
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, async (request, context, cancellationToken) =>
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
                CreateRequest("req-pending-fence", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
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
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, static async (request, context, cancellationToken) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellationToken);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);

            var dispatchTask = dispatcher.DispatchAsync(
                CreateRequest("req-completed-fence", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
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
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, static async (request, context, cancellationToken) =>
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
                    CreateRequest("req-completed-hard-deadline", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 5000 }),
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
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, async (request, context, cancellationToken) =>
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
                    CreateRequest("req-terminal-cancel", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
                    cancellationTokenSource.Token).AsUniTask();
            }, "Recoverable terminal cancellation", AsyncWaitTimeout);

            Assert.That(store.CompletedWriteCallCount, Is.EqualTo(1));
            Assert.That(store.CompletedWriteObservedCancellation, Is.False);
            Assert.That(store.CompletedResponse, Is.Not.Null);
            Assert.That(store.CompletedResponse.RequestId, Is.EqualTo("req-terminal-cancel"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableHandlerReturnsError_PersistsErrorAsCompletedBeforeReturning () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            var terminalErrorResponse = new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "req-terminal-error",
                Status: IpcProtocol.StatusError,
                Payload: default,
                Errors: new[] { new IpcError(UcliCoreErrorCodes.InternalError, "terminal failure", null) });
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, async (_, context, cancellationToken) =>
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
                    CreateRequest("req-terminal-error", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 }),
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
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, static (_, _, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse("unexpected")));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest("req-invalid-record", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

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
            var handler = new StubMethodHandler(IpcMethodNames.Ping, static (_, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse("req-canceled")));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler });
            var request = CreateRequest("req-canceled", IpcMethodNames.Ping, new IpcPingRequest("tests"));
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await dispatcher.DispatchAsync(request, cancellationTokenSource.Token).AsUniTask();
            }, "Canceled IPC method dispatch", AsyncWaitTimeout);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        });

        private static IpcRequest CreateRequest (
            string requestId,
            string method,
            object payload)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: "session-token",
                Method: method,
                Payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: IpcResponseMode.Single);
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

        private static IpcResponse CreateSuccessResponse (string requestId)
        {
            return new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                Status: IpcProtocol.StatusOk,
                Payload: IpcPayloadCodec.SerializeToElement(new { ok = true }),
                Errors: Array.Empty<IpcError>());
        }

        private sealed class StubMethodHandler : IUnityIpcMethodHandler
        {
            private readonly Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handle;

            public StubMethodHandler (
                string method,
                Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public int CallCount { get; private set; }

            public string Method { get; }

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
                string method,
                Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public string Method { get; }

            public ValueTask<IpcResponse> HandleAsync (
                IpcRequest request,
                CancellationToken cancellationToken)
            {
                return handle(request, cancellationToken);
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
                string method,
                Func<IpcRequest, RecoverableIpcOperationContext, CancellationToken, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public int PlainCallCount { get; private set; }

            public int RecoverableCallCount { get; private set; }

            public string Method { get; }

            public string RecoverableRequestPayloadHash { get; set; } = "stub-recoverable-hash";

            public bool TryCreateRecoverableRequestPayloadHash (
                IpcRequest request,
                out string requestPayloadHash,
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

            public string ExpectedReadRequestPayloadHash { get; set; }

            public string LastReadRequestPayloadHash { get; private set; }

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
                string method,
                string requestId,
                string requestPayloadHash,
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

                if (!string.IsNullOrWhiteSpace(ExpectedReadRequestPayloadHash)
                    && !string.Equals(ExpectedReadRequestPayloadHash, requestPayloadHash, StringComparison.Ordinal))
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
                string method,
                string requestId,
                string requestPayloadHash,
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
                string method,
                string requestId,
                string requestPayloadHash,
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
