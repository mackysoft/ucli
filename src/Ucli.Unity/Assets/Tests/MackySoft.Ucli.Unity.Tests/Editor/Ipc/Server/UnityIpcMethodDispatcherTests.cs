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
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcMethodDispatcherTests
    {
        private const string CooperativeTimeoutMessage = "Compile handler reached its execution deadline.";

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

        [TestCase((UnityIpcMethod)0)]
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
        public IEnumerator Dispatch_WhenMethodIsNotRegistered_ReturnsCorrelatedMethodNotSupportedError () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = CreateDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new StubMethodHandler(UnityIpcMethod.Ping, static (_, _) =>
                        new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid()))),
                });
            var requestId = Guid.NewGuid();
            var request = CreateRequest(requestId, UnityIpcMethod.Shutdown, new UcliEmptyArgs());

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Unregistered IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.RequestId, Is.EqualTo(requestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcProtocolErrorCodes.IpcMethodNotSupported));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DispatchStreaming_WhenMethodIsNotRegistered_ReturnsCorrelatedMethodNotSupportedError () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = CreateDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new StubMethodHandler(UnityIpcMethod.Ping, static (_, _) =>
                        new ValueTask<IpcResponse>(CreateSuccessResponse(Guid.NewGuid()))),
                });
            var requestId = Guid.NewGuid();
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: "session-token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);

            var response = await TestAwaiter.WaitAsync(
                DispatchStreamingAsync(
                    dispatcher,
                    request,
                    new UnexpectedStreamFrameWriter(),
                    CancellationToken.None).AsUniTask(),
                "Unregistered streaming IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.RequestId, Is.EqualTo(requestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
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
                    new StubMethodHandler(UnityIpcMethod.Ping, static (_, _) =>
                        throw new InvalidOperationException("dispatcher-test")),
                });
            var request = CreateRequest(Guid.NewGuid(), UnityIpcMethod.Ping, new IpcPingRequest("tests"));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Throwing IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(response.Errors[0].Message, Does.Contain("dispatcher-test"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenControlPlaneCapacityIsExhausted_ReturnsEditorBusy () => UniTask.ToCoroutine(async () =>
        {
            var handler = new StubControlPlaneMethodHandler(
                UnityIpcMethod.Ping,
                static (request, _) => new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                new RecordingMutationExecutor(),
                new CapacityExceededControlPlaneExecutor(),
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);
            var request = CreateRequest(Guid.NewGuid(), UnityIpcMethod.Ping, new IpcPingRequest("tests"));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Exhausted control-plane IPC dispatch",
                AsyncWaitTimeout);

            Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors, Has.Count.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
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
                    DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                    "Uncorrelated IPC method response",
                    AsyncWaitTimeout);

                Assert.That(response.RequestId, Is.EqualTo(requestId));
                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
                Assert.That(handler.CallCount, Is.EqualTo(1));
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMethodIsRegistered_DelegatesToHandler () => UniTask.ToCoroutine(async () =>
        {
            var handler = new StubMethodHandler(UnityIpcMethod.Ping, static (request, cancellation) =>
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId));
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler });
            var request = CreateRequest(Guid.NewGuid(), UnityIpcMethod.Ping, new IpcPingRequest("tests"));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Successful IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenOpsReadIsRequested_UsesIndependentControlExecutor () => UniTask.ToCoroutine(async () =>
        {
            var mutationExecutor = new RecordingMutationExecutor();
            var controlExecutor = new RecordingControlPlaneExecutor();
            var handler = new OpsReadUnityIpcMethodHandler(
                UcliOperationCatalogSnapshotBuilder.Build(Array.Empty<UcliOperationRegistration>()),
                new StubUnityEditorReadinessGate());
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                mutationExecutor,
                controlExecutor,
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher,
                    CreateRequest(Guid.NewGuid(), UnityIpcMethod.OpsRead, new IpcOpsReadRequest()),
                    CancellationToken.None).AsUniTask(),
                "Ops-read control-plane IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(controlExecutor.CallCount, Is.EqualTo(1));
            Assert.That(mutationExecutor.CallCount, Is.Zero);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenOpsReadAwaitsReadiness_PingRemainsResponsive () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var opsReadHandler = new OpsReadUnityIpcMethodHandler(
                UcliOperationCatalogSnapshotBuilder.Build(Array.Empty<UcliOperationRegistration>()),
                readinessGate);
            var pingHandler = new StubControlPlaneMethodHandler(
                UnityIpcMethod.Ping,
                static (request, cancellation) =>
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    return new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId));
                });
            using var controlExecutor = new UnityControlPlaneRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnityControlPlaneRequestExecutor.DefaultMaxConcurrentInvocations);
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { opsReadHandler, pingHandler },
                new RecordingMutationExecutor(),
                controlExecutor,
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);
            var opsReadTask = DispatchAsync(
                dispatcher,
                CreateRequest(
                    Guid.NewGuid(),
                    UnityIpcMethod.OpsRead,
                    new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true)),
                CancellationToken.None);

            await TestAwaiter.WaitAsync(
                readinessGate.WaitObserved.AsUniTask(),
                "Pending ops.read readiness wait",
                AsyncWaitTimeout);
            var pingResponse = await TestAwaiter.WaitAsync(
                DispatchAsync(
                    dispatcher,
                    CreateRequest(Guid.NewGuid(), UnityIpcMethod.Ping, new IpcPingRequest("tests")),
                    CancellationToken.None).AsUniTask(),
                "Ping while ops.read is awaiting readiness",
                AsyncWaitTimeout);

            Assert.That(pingResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(opsReadTask.IsCompleted, Is.False);

            readinessGate.Release();
            var opsReadResponse = await TestAwaiter.WaitAsync(
                opsReadTask.AsUniTask(),
                "ops.read completion after readiness",
                AsyncWaitTimeout);
            Assert.That(opsReadResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMutationLaneIsQuarantined_ControlPlaneRemainsResponsive () => UniTask.ToCoroutine(async () =>
        {
            using var mutationCancellation = new CancellationTokenSource();
            using var mutationExecutor = new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations);
            using var controlExecutor = new UnityControlPlaneRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnityControlPlaneRequestExecutor.DefaultMaxConcurrentInvocations);
            var mutationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseMutation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var mutationHandler = new StubMethodHandler(UnityIpcMethod.Compile, async (request, cancellation) =>
            {
                var mutationActivity = mutationExecutor.BeginMutation();
                mutationStarted.TrySetResult(true);
                try
                {
                    await releaseMutation.Task;
                    cancellation.Token.ThrowIfCancellationRequested();
                    return CreateSuccessResponse(request.RequestId);
                }
                finally
                {
                    mutationActivity.Complete();
                }
            });
            var pingHandler = new StubControlPlaneMethodHandler(
                UnityIpcMethod.Ping,
                static (request, _) => new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { mutationHandler, pingHandler },
                mutationExecutor,
                controlExecutor,
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);

            var mutationTask = DispatchAsync(
                dispatcher,
                CreateRequest(
                    Guid.NewGuid(),
                    UnityIpcMethod.Compile,
                    new IpcCompileRequest(Guid.NewGuid())),
                mutationCancellation.Token);
            try
            {
                await TestAwaiter.WaitAsync(
                    mutationStarted.Task,
                    "Non-cooperative mutation start",
                    AsyncWaitTimeout);
                mutationCancellation.Cancel();
                await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
                {
                    await mutationTask.AsUniTask();
                }, "Non-cooperative mutation cancellation", AsyncWaitTimeout);

                Assert.That(mutationExecutor.IsQuarantined, Is.True);
                Assert.That(mutationExecutor.HasUnfinishedWork, Is.True);

                var pingResponse = await TestAwaiter.WaitAsync(
                    DispatchAsync(
                        dispatcher,
                        CreateRequest(Guid.NewGuid(), UnityIpcMethod.Ping, new IpcPingRequest("tests")),
                        CancellationToken.None).AsUniTask(),
                    "Control-plane dispatch while mutation lane is quarantined",
                    AsyncWaitTimeout);

                Assert.That(pingResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            }
            finally
            {
                mutationCancellation.Cancel();
                releaseMutation.TrySetResult(true);
                await TestAwaiter.WaitAsync(
                    mutationExecutor.WaitForRetirementAsync().AsUniTask(),
                    "Quarantined mutation retirement",
                    AsyncWaitTimeout);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenDeadlineExpiredBeforeEntry_ReturnsCorrelatedTimeoutWithoutCallingHandler () => UniTask.ToCoroutine(async () =>
        {
            var handler = new StubMethodHandler(UnityIpcMethod.Ping, static (request, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId)));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler });
            var expiredAtUtc = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(1);
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "session-token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Ping),
                payload: IpcPayloadCodec.SerializeToElement(new IpcPingRequest("tests")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: expiredAtUtc,
                requestDeadlineRemainingMilliseconds: 1);

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Expired IPC dispatch",
                AsyncWaitTimeout);

            Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(response.Errors, Has.Count.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
            Assert.That(handler.CallCount, Is.Zero);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMutationQueueOutlivesExecutionDeadline_ReturnsIpcTimeoutWithoutCallingHandler () => UniTask.ToCoroutine(async () =>
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
                new IpcCompileRequest(Guid.Parse("00000000-0000-0000-0000-000000000612")),
                requestDuration: TimeSpan.FromMilliseconds(50));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Mutation queue execution deadline",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
            Assert.That(handler.CallCount, Is.Zero);
            Assert.That(mutationExecutor.CallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenHandlerReturnsMethodTimeoutAtExecutionDeadline_PreservesTerminalResponseAfterExecutorCancellationCheck () => UniTask.ToCoroutine(async () =>
        {
            var handler = new StubMethodHandler(UnityIpcMethod.Compile, static async (request, cancellation) =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellation.Token);
                    throw new InvalidOperationException("The deadline cancellation was not observed.");
                }
                catch (OperationCanceledException) when (
                    cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        IpcTransportErrorCodes.IpcTimeout,
                        CooperativeTimeoutMessage,
                        null);
                }
            });
            var executor = new PostExecutionCancellationExecutor();
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                executor,
                executor,
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.Compile,
                new IpcCompileRequest(Guid.Parse("00000000-0000-0000-0000-000000000613")),
                requestDuration: TimeSpan.FromMilliseconds(50));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Cooperative method execution deadline",
                AsyncWaitTimeout);

            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
            Assert.That(response.Errors[0].Message, Is.EqualTo(CooperativeTimeoutMessage));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenNonCooperativeHandlerSucceedsAfterExecutionDeadline_ReturnsIpcTimeout () => UniTask.ToCoroutine(async () =>
        {
            var executor = new HoldResultUntilCancellationExecutor();
            var handler = new StubMethodHandler(UnityIpcMethod.Compile, static async (request, _) =>
            {
                await Task.Yield();
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                executor,
                executor,
                recoverableOperationStore: null,
                NoOpDaemonLogger.Instance);
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.Compile,
                new IpcCompileRequest(Guid.Parse("00000000-0000-0000-0000-000000000614")),
                requestDuration: TimeSpan.FromMilliseconds(500));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Non-cooperative late success",
                AsyncWaitTimeout);

            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableHandlerSucceedsBeforeLaneObservesExecutionDeadline_PersistsTerminalResponseBeforeReturningTimeout () => UniTask.ToCoroutine(async () =>
        {
            var executor = new HoldResultUntilCancellationExecutor();
            var store = new StubRecoverableIpcOperationStore();
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static async (request, context, cancellation) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellation.Token);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                await Task.Yield();
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[] { handler },
                executor,
                executor,
                store,
                NoOpDaemonLogger.Instance);
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.PlayEnter,
                new IpcPlayEnterRequest(),
                requestDuration: TimeSpan.FromMilliseconds(500));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Recoverable non-cooperative late success",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
            Assert.That(store.PendingWriteCallCount, Is.EqualTo(1));
            Assert.That(store.CompletedWriteCallCount, Is.EqualTo(1));
            Assert.That(store.CompletedResponse.RequestId, Is.EqualTo(request.RequestId));
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
            var request = CreateRequest(
                requestId,
                UnityIpcMethod.PlayEnter,
                new IpcPlayEnterRequest(),
                requestDuration: TimeSpan.FromMilliseconds(1000));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
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
            var request = CreateRequest(
                requestId,
                UnityIpcMethod.PlayEnter,
                new IpcPlayEnterRequest(),
                requestDuration: TimeSpan.FromMilliseconds(1000));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Mismatched completed recoverable response",
                AsyncWaitTimeout);

            Assert.That(response.RequestId, Is.EqualTo(requestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
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
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.PlayEnter,
                new IpcPlayEnterRequest(),
                requestDuration: TimeSpan.FromMilliseconds(1000));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Hash-mismatched recoverable IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(handler.RecoverableCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableStoreExistsAndHandlerDoesNotSupportRecovery_DelegatesNormally () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            var handler = new StubMethodHandler(UnityIpcMethod.PlayEnter, static (request, cancellation) =>
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId));
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.PlayEnter,
                new IpcPlayEnterRequest(),
                requestDuration: TimeSpan.FromMilliseconds(1000));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Plain method handler with recoverable store dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(store.ReadCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableHandlerCreatesPendingRecord_WritesCompletedResponse () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static async (request, context, cancellation) =>
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var result = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellation.Token);
                Assert.That(result.IsSuccess, Is.True, result.ErrorMessage);
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.PlayEnter,
                new IpcPlayEnterRequest(),
                requestDuration: TimeSpan.FromMilliseconds(1000));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Pending recoverable IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static async (_, context, cancellation) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellation.Token);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                return CreateSuccessResponse(Guid.NewGuid());
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest(
                requestId,
                UnityIpcMethod.PlayEnter,
                new IpcPlayEnterRequest(),
                requestDuration: TimeSpan.FromMilliseconds(1000));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Mismatched recoverable handler response",
                AsyncWaitTimeout);

            Assert.That(response.RequestId, Is.EqualTo(requestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
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

            var dispatchTask = DispatchAsync(dispatcher,
                CreateRequest(
                    Guid.NewGuid(),
                    UnityIpcMethod.PlayEnter,
                    new IpcPlayEnterRequest(),
                    requestDuration: TimeSpan.FromMilliseconds(1000)),
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
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
                DispatchAsync(dispatcher,
                    CreateRequest(
                        Guid.NewGuid(),
                        UnityIpcMethod.PlayEnter,
                        new IpcPlayEnterRequest(),
                        requestDuration: TimeSpan.FromMilliseconds(1000)),
                    CancellationToken.None).AsUniTask(),
                "Recoverable maintenance report",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, async (request, context, cancellation) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellation.Token);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                mutationPerformed = true;
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);

            var dispatchTask = DispatchAsync(dispatcher,
                CreateRequest(
                    Guid.NewGuid(),
                    UnityIpcMethod.PlayEnter,
                    new IpcPlayEnterRequest(),
                    requestDuration: TimeSpan.FromMilliseconds(1000)),
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
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static async (request, context, cancellation) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellation.Token);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);

            var dispatchTask = DispatchAsync(dispatcher,
                CreateRequest(
                    Guid.NewGuid(),
                    UnityIpcMethod.PlayEnter,
                    new IpcPlayEnterRequest(),
                    requestDuration: TimeSpan.FromMilliseconds(1000)),
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
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCompletedPersistenceIgnoresCancellation_ReturnsAtPlannedPersistenceCutoffAndObservesLateTask () => UniTask.ToCoroutine(async () =>
        {
            var completedWritePermission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new StubRecoverableIpcOperationStore
            {
                CompletedWritePermission = completedWritePermission.Task,
            };
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, static async (request, context, cancellation) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellation.Token);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                return CreateSuccessResponse(request.RequestId);
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher,
                    CreateRequest(
                        Guid.NewGuid(),
                        UnityIpcMethod.PlayEnter,
                        new IpcPlayEnterRequest(),
                        requestDuration: TimeSpan.FromMilliseconds(500)),
                    CancellationToken.None).AsUniTask(),
                "Completed persistence hard deadline",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(response.Errors[0].Message, Does.Contain("persistence cutoff"));
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
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, async (request, context, cancellation) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellation.Token);
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
                await DispatchAsync(dispatcher,
                    CreateRequest(
                        requestId,
                        UnityIpcMethod.PlayEnter,
                        new IpcPlayEnterRequest(),
                        requestDuration: TimeSpan.FromMilliseconds(1000)),
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
                status: IpcResponseStatus.Error,
                payload: IpcPayloadCodec.SerializeToElement(new { }),
                errors: new[] { new IpcError(UcliCoreErrorCodes.InternalError, "terminal failure", null) });
            var handler = new StubRecoverableMethodHandler(UnityIpcMethod.PlayEnter, async (_, context, cancellation) =>
            {
                var pendingResult = await context.MarkPendingAsync(
                    new { checkpoint = "before" },
                    cancellation.Token);
                Assert.That(pendingResult.IsSuccess, Is.True, pendingResult.ErrorMessage);
                return terminalErrorResponse;
            });
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler }, store);

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher,
                    CreateRequest(
                        requestId,
                        UnityIpcMethod.PlayEnter,
                        new IpcPlayEnterRequest(),
                        requestDuration: TimeSpan.FromMilliseconds(1000)),
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
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.PlayEnter,
                new IpcPlayEnterRequest(),
                requestDuration: TimeSpan.FromMilliseconds(1000));

            var response = await TestAwaiter.WaitAsync(
                DispatchAsync(dispatcher, request, CancellationToken.None).AsUniTask(),
                "Invalid recoverable IPC record dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
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
                await DispatchAsync(dispatcher, request, cancellationTokenSource.Token).AsUniTask();
            }, "Canceled IPC method dispatch", AsyncWaitTimeout);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        });

        private static IpcRequestEnvelope CreateRequest (
            Guid requestId,
            UnityIpcMethod method,
            object payload,
            TimeSpan? requestDuration = null)
        {
            var duration = requestDuration ?? TimeSpan.FromSeconds(30);
            return new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: "session-token",
                method: ContractLiteralCodec.ToValue(method),
                payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow + duration,
                requestDeadlineRemainingMilliseconds: checked((int)Math.Ceiling(duration.TotalMilliseconds)));
        }

        private static async Task<IpcResponse> DispatchAsync (
            UnityIpcMethodDispatcher dispatcher,
            IpcRequestEnvelope request,
            CancellationToken cancellationToken)
        {
            using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                request,
                cancellationToken,
                TimeSpan.FromSeconds(1));
            Assert.That(
                ContractLiteralCodec.TryParse(request.Method, out UnityIpcMethod method),
                Is.True);
            var validatedRequest = CreateValidatedRequest(request, method, IpcResponseMode.Single);
            return await dispatcher.DispatchAsync(
                validatedRequest,
                phaseScope);
        }

        private static async Task<IpcResponse> DispatchStreamingAsync (
            UnityIpcMethodDispatcher dispatcher,
            IpcRequestEnvelope request,
            IIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken)
        {
            using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                request,
                cancellationToken,
                TimeSpan.FromSeconds(1));
            Assert.That(
                ContractLiteralCodec.TryParse(request.Method, out UnityIpcMethod method),
                Is.True);
            var validatedRequest = CreateValidatedRequest(request, method, IpcResponseMode.Stream);
            return await dispatcher.DispatchStreamingAsync(
                validatedRequest,
                streamWriter,
                phaseScope);
        }

        private static ValidatedUnityIpcRequest CreateValidatedRequest (
            IpcRequestEnvelope request,
            UnityIpcMethod method,
            IpcResponseMode responseMode)
        {
            return ValidatedUnityIpcRequestTestFactory.Create(request, method, responseMode);
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
                status: IpcResponseStatus.Ok,
                payload: IpcPayloadCodec.SerializeToElement(new { ok = true }),
                errors: Array.Empty<IpcError>());
        }

        private sealed class StubMethodHandler : IUnityIpcMethodHandler
        {
            private readonly Func<ValidatedUnityIpcRequest, IpcRequestCancellation, ValueTask<IpcResponse>> handle;

            public StubMethodHandler (
                UnityIpcMethod method,
                Func<ValidatedUnityIpcRequest, IpcRequestCancellation, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public int CallCount { get; private set; }

            public UnityIpcMethod Method { get; }

            public ValueTask<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestCancellation cancellation)
            {
                CallCount++;
                return handle(request, cancellation);
            }
        }

        private sealed class StubControlPlaneMethodHandler : IUnityControlPlaneIpcMethodHandler
        {
            private readonly Func<ValidatedUnityIpcRequest, IpcRequestCancellation, ValueTask<IpcResponse>> handle;

            public StubControlPlaneMethodHandler (
                UnityIpcMethod method,
                Func<ValidatedUnityIpcRequest, IpcRequestCancellation, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public UnityIpcMethod Method { get; }

            public ValueTask<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestCancellation cancellation)
            {
                return handle(request, cancellation);
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

        private sealed class HoldResultUntilCancellationExecutor :
            IUnityMainThreadRequestExecutor,
            IUnityControlPlaneRequestExecutor
        {
            public async Task<T> ExecuteAsync<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await workItem();

                // Deliberately allow the cancellation continuation to run inline. The test models
                // a lane that has already produced a terminal response when the execution cutoff
                // is observed, so completion persistence must begin before the later persistence
                // cutoff instead of depending on thread-pool scheduling between both cutoffs.
                var cancellationSource = new TaskCompletionSource<T>();
                using var cancellationRegistration = cancellationToken.Register(
                    static state => ((TaskCompletionSource<T>)state).TrySetCanceled(),
                    cancellationSource);
                return await cancellationSource.Task.ConfigureAwait(false);
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

        private sealed class CapacityExceededControlPlaneExecutor : IUnityControlPlaneRequestExecutor
        {
            public Task<T> ExecuteAsync<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException<T>(new UnityControlPlaneCapacityExceededException(1));
            }
        }

        private sealed class StubRecoverableMethodHandler : IRecoverableUnityIpcMethodHandler
        {
            private readonly Func<ValidatedUnityIpcRequest, RecoverableIpcOperationContext, IpcRequestCancellation, ValueTask<IpcResponse>> handle;

            public StubRecoverableMethodHandler (
                UnityIpcMethod method,
                Func<ValidatedUnityIpcRequest, RecoverableIpcOperationContext, IpcRequestCancellation, ValueTask<IpcResponse>> handle)
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
                ValidatedUnityIpcRequest request,
                out Sha256Digest requestPayloadHash,
                out IpcResponse errorResponse)
            {
                requestPayloadHash = RecoverableRequestPayloadHash;
                errorResponse = null;
                return true;
            }

            public ValueTask<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestCancellation cancellation)
            {
                PlainCallCount++;
                return handle(request, null, cancellation);
            }

            public ValueTask<IpcResponse> HandleRecoverableAsync (
                ValidatedUnityIpcRequest request,
                RecoverableIpcOperationContext context,
                IpcRequestCancellation cancellation)
            {
                RecoverableCallCount++;
                return handle(request, context, cancellation);
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
