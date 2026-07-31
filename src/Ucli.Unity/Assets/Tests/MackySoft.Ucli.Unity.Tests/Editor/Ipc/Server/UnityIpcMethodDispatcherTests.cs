using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
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
        public IEnumerator Dispatch_WhenHandlerCompletesOffCapturedContext_ReturnsResponse () => UniTask.ToCoroutine(async () =>
        {
            var requestId = Guid.Parse("00000000-0000-0000-0000-000000000617");
            var handlerResponseSource = new TaskCompletionSource<IpcResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new StubMethodHandler(
                UnityIpcMethod.Ping,
                (_, _) => new ValueTask<IpcResponse>(handlerResponseSource.Task));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler });
            var request = CreateRequest(
                requestId,
                UnityIpcMethod.Ping,
                new IpcPingRequest("tests"));
            using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                request,
                CancellationToken.None,
                TimeSpan.FromSeconds(1));

            var dispatchTask = StartWithDroppingSynchronizationContext(
                () => dispatcher.DispatchAsync(
                    CreateValidatedRequest(
                        request,
                        UnityIpcMethod.Ping,
                        IpcResponseMode.Single),
                    phaseScope),
                out var droppingSynchronizationContext);
            handlerResponseSource.TrySetResult(CreateSuccessResponse(requestId));

            var response = await TestAwaiter.WaitAsync(
                dispatchTask.AsUniTask(),
                "Off-context IPC method response",
                AsyncWaitTimeout);

            Assert.That(droppingSynchronizationContext.PostCallCount, Is.Zero);
            Assert.That(response.RequestId, Is.EqualTo(requestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
                method: TextVocabulary.GetText(UnityIpcMethod.Shutdown),
                payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
                responseMode: TextVocabulary.GetText(IpcResponseMode.Stream),
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
        public IEnumerator DispatchStreaming_WhenHandlerCompletesOffCapturedContext_ReturnsResponse () => UniTask.ToCoroutine(async () =>
        {
            var requestId = Guid.Parse("00000000-0000-0000-0000-000000000618");
            var handlerResponseSource = new TaskCompletionSource<IpcResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new StubStreamingMethodHandler(
                UnityIpcMethod.TestRun,
                (_, _, _) => new ValueTask<IpcResponse>(handlerResponseSource.Task));
            var dispatcher = CreateDispatcher(new IUnityIpcMethodHandler[] { handler });
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: "session-token",
                method: TextVocabulary.GetText(UnityIpcMethod.TestRun),
                payload: IpcPayloadCodec.SerializeToElement(new IpcTestRunRequest(
                    TestPlatform: TestRunPlatformCodec.EditMode,
                    TestFilter: null,
                    TestCategories: Array.Empty<string>(),
                    AssemblyNames: Array.Empty<string>(),
                    RunId: Guid.Parse("00000000-0000-0000-0000-000000000619"),
                    FailFast: false)),
                responseMode: TextVocabulary.GetText(IpcResponseMode.Stream),
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
            using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                request,
                CancellationToken.None,
                TimeSpan.FromSeconds(1));

            var dispatchTask = StartWithDroppingSynchronizationContext(
                () => dispatcher.DispatchStreamingAsync(
                    CreateValidatedRequest(
                        request,
                        UnityIpcMethod.TestRun,
                        IpcResponseMode.Stream),
                    new UnexpectedStreamFrameWriter(),
                    phaseScope),
                out var droppingSynchronizationContext);
            handlerResponseSource.TrySetResult(CreateSuccessResponse(requestId));

            var response = await TestAwaiter.WaitAsync(
                dispatchTask.AsUniTask(),
                "Off-context streaming IPC method response",
                AsyncWaitTimeout);

            Assert.That(droppingSynchronizationContext.PostCallCount, Is.Zero);
            Assert.That(response.RequestId, Is.EqualTo(requestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
                new CapacityExceededControlPlaneExecutor());
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
                controlExecutor);

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
                controlExecutor);
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
                controlExecutor);

            var mutationTask = DispatchAsync(
                dispatcher,
                CreateRequest(
                    Guid.NewGuid(),
                    UnityIpcMethod.Compile,
                    new UcliEmptyArgs()),
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
                method: TextVocabulary.GetText(UnityIpcMethod.Ping),
                payload: IpcPayloadCodec.SerializeToElement(new IpcPingRequest("tests")),
                responseMode: TextVocabulary.GetText(IpcResponseMode.Single),
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
                new InlineRequestExecutor());
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.Compile,
                new UcliEmptyArgs(),
                requestDuration: TimeSpan.FromMilliseconds(500));

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
                executor);
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.Compile,
                new UcliEmptyArgs(),
                requestDuration: TimeSpan.FromMilliseconds(500));

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
                executor);
            var request = CreateRequest(
                Guid.NewGuid(),
                UnityIpcMethod.Compile,
                new UcliEmptyArgs(),
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
                method: TextVocabulary.GetText(method),
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
                TextVocabulary.TryGetValue(request.Method, out UnityIpcMethod method),
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
                TextVocabulary.TryGetValue(request.Method, out UnityIpcMethod method),
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

        private static Task<T> StartWithDroppingSynchronizationContext<T> (
            Func<Task<T>> start,
            out DroppingSynchronizationContext droppingSynchronizationContext)
        {
            var originalSynchronizationContext = SynchronizationContext.Current;
            droppingSynchronizationContext = new DroppingSynchronizationContext();
            try
            {
                SynchronizationContext.SetSynchronizationContext(droppingSynchronizationContext);
                return start();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(originalSynchronizationContext);
            }
        }

        private static UnityIpcMethodDispatcher CreateDispatcher (IUnityIpcMethodHandler[] handlers)
        {
            var executor = new InlineRequestExecutor();
            return new UnityIpcMethodDispatcher(
                handlers,
                executor,
                executor);
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

        private sealed class StubStreamingMethodHandler : IStreamingUnityIpcMethodHandler
        {
            private readonly Func<ValidatedUnityIpcRequest, IIpcStreamFrameWriter, IpcRequestCancellation, ValueTask<IpcResponse>> handle;

            public StubStreamingMethodHandler (
                UnityIpcMethod method,
                Func<ValidatedUnityIpcRequest, IIpcStreamFrameWriter, IpcRequestCancellation, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public UnityIpcMethod Method { get; }

            public ValueTask<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestCancellation cancellation)
            {
                throw new InvalidOperationException("Streaming dispatcher test must not use single-response handling.");
            }

            public ValueTask<IpcResponse> HandleStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestCancellation cancellation)
            {
                return handle(request, streamWriter, cancellation);
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

                // Hold a produced result until the lane observes request cancellation.
                var cancellationSource = new TaskCompletionSource<T>();
                using var cancellationRegistration = cancellationToken.Register(
                    static state => ((TaskCompletionSource<T>)state).TrySetCanceled(),
                    cancellationSource);
                return await cancellationSource.Task.ConfigureAwait(false);
            }
        }

        private sealed class DroppingSynchronizationContext : SynchronizationContext
        {
            private int postCallCount;

            public int PostCallCount => Volatile.Read(ref postCallCount);

            public override void Post (
                SendOrPostCallback d,
                object state)
            {
                Interlocked.Increment(ref postCallCount);
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

    }
}
