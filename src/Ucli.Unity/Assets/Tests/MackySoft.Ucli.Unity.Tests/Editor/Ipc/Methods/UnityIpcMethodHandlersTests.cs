using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcMethodHandlersTests
    {
        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PingHandler_WhenPayloadIsValid_ReturnsOkResponse () => UniTask.ToCoroutine(async () =>
        {
            var handler = new PingUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                new StubUnityEditorReadinessGate(),
                "project-fingerprint",
                NoOpDaemonLogger.Instance);
            var request = CreatePingRequest(Guid.NewGuid(), new IpcPingRequest("client"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPingResponse payload, out _), Is.True);
            Assert.That(payload.ServerVersion, Is.EqualTo("1.2.3"));
            Assert.That(payload.EditorMode, Is.EqualTo("batchmode"));
            Assert.That(payload.ProjectFingerprint, Is.EqualTo("project-fingerprint"));
            Assert.That(payload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(payload.BlockingReason, Is.Null);
            Assert.That(payload.CompileGeneration, Is.EqualTo("1"));
            Assert.That(payload.DomainReloadGeneration, Is.EqualTo("1"));
            Assert.That(payload.CanAcceptExecutionRequests, Is.True);
            Assert.That(payload.PlayMode, Is.Not.Null);
            Assert.That(payload.PlayMode!.State, Is.EqualTo("stopped"));
            Assert.That(payload.PlayMode.Transition, Is.EqualTo("none"));
            Assert.That(payload.PlayMode.IsPlaying, Is.False);
            Assert.That(payload.PlayMode.IsPlayingOrWillChangePlaymode, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PingHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = new PingUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                new StubUnityEditorReadinessGate(),
                "project-fingerprint",
                NoOpDaemonLogger.Instance);
            var request = CreatePingRequest(Guid.NewGuid(), 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PingHandler_WhenEditorModeIsGui_ReturnsGuiEditorMode () => UniTask.ToCoroutine(async () =>
        {
            var handler = new PingUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                new StubUnityEditorReadinessGate(DaemonEditorMode.Gui),
                "project-fingerprint",
                NoOpDaemonLogger.Instance);
            var request = CreatePingRequest(Guid.NewGuid(), new IpcPingRequest("client"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPingResponse payload, out _), Is.True);
            Assert.That(payload.EditorMode, Is.EqualTo("gui"));
            Assert.That(payload.ProjectFingerprint, Is.EqualTo("project-fingerprint"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PingHandler_WhenStartupIsPending_DoesNotConsumeStarting () => UniTask.ToCoroutine(async () =>
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 0,
                domainReloadGeneration: 1,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true);
            var handler = new PingUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                CreateReadinessGate(
                    telemetryState,
                    static () => false,
                    static () => false,
                    static () => false),
                "project-fingerprint",
                NoOpDaemonLogger.Instance);

            var firstResponse = await handler.HandleAsync(CreatePingRequest(Guid.NewGuid(), new IpcPingRequest("client")), CancellationToken.None);
            var secondResponse = await handler.HandleAsync(CreatePingRequest(Guid.NewGuid(), new IpcPingRequest("client")), CancellationToken.None);

            Assert.That(IpcPayloadCodec.TryDeserialize(firstResponse.Payload, out IpcPingResponse firstPayload, out _), Is.True);
            Assert.That(IpcPayloadCodec.TryDeserialize(secondResponse.Payload, out IpcPingResponse secondPayload, out _), Is.True);
            Assert.That(firstPayload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(secondPayload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));

            telemetryState.ObserveEditorUpdate(
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false);
            var readyResponse = await handler.HandleAsync(CreatePingRequest(Guid.NewGuid(), new IpcPingRequest("client")), CancellationToken.None);

            Assert.That(IpcPayloadCodec.TryDeserialize(readyResponse.Payload, out IpcPingResponse readyPayload, out _), Is.True);
            Assert.That(readyPayload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(readyPayload.CanAcceptExecutionRequests, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PingHandler_WhenPlaymodeIsActive_ReturnsPlaymodeSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 0,
                domainReloadGeneration: 1,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);
            var handler = new PingUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                CreateReadinessGate(
                    telemetryState,
                    static () => false,
                    static () => false,
                    static () => true),
                "project-fingerprint",
                NoOpDaemonLogger.Instance);

            var response = await handler.HandleAsync(CreatePingRequest(Guid.NewGuid(), new IpcPingRequest("client")), CancellationToken.None);

            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPingResponse payload, out _), Is.True);
            Assert.That(payload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Playmode));
            Assert.That(payload.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.PlayMode));
            Assert.That(payload.CanAcceptExecutionRequests, Is.False);
            Assert.That(payload.PlayMode, Is.Not.Null);
            Assert.That(payload.PlayMode!.State, Is.EqualTo("playing"));
            Assert.That(payload.PlayMode.Transition, Is.EqualTo("none"));
            Assert.That(payload.PlayMode.IsPlaying, Is.True);
            Assert.That(payload.PlayMode.IsPlayingOrWillChangePlaymode, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator GuiRebootstrapHandler_WhenReplaceExistingSessionChanges_PassesReplacementScopeToBootstrap () => UniTask.ToCoroutine(async () =>
        {
            var bootstrapStarter = new RecordingUnityGuiBootstrapStarter();
            var handler = new GuiRebootstrapUnityIpcMethodHandler(
                bootstrapStarter: bootstrapStarter,
                projectFingerprint: "project-fingerprint",
                daemonLogger: NoOpDaemonLogger.Instance);

            var falseResponse = await handler.HandleAsync(
                CreateRequest(
                    Guid.NewGuid(),
                    IpcMethodNames.GuiRebootstrap,
                    new IpcGuiRebootstrapRequest(
                        ProjectFingerprint: "project-fingerprint",
                        ReplaceExistingSession: false)),
                CancellationToken.None);
            var trueResponse = await handler.HandleAsync(
                CreateRequest(
                    Guid.NewGuid(),
                    IpcMethodNames.GuiRebootstrap,
                    new IpcGuiRebootstrapRequest(
                        ProjectFingerprint: "project-fingerprint",
                        ReplaceExistingSession: true)),
                CancellationToken.None);

            Assert.That(falseResponse.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(trueResponse.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(bootstrapStarter.BootstrapArguments, Is.EqualTo(new IpcGuiBootstrapArguments[] { null, null }));
            Assert.That(
                bootstrapStarter.SessionReplacementScopes,
                Is.EqualTo(new[]
                {
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession,
                    UnityGuiSessionReplacementScope.AnyCurrentProcessSession,
                }));
            Assert.That(
                bootstrapStarter.CancellationTokens,
                Is.EqualTo(new[] { CancellationToken.None, CancellationToken.None }));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator GuiRebootstrapHandler_WhenRequestIsCanceled_CancelsBootstrapAndPropagatesCancellation () => UniTask.ToCoroutine(async () =>
        {
            var bootstrapStarter = new CancellationObservingUnityGuiBootstrapStarter();
            var handler = new GuiRebootstrapUnityIpcMethodHandler(
                bootstrapStarter,
                "project-fingerprint",
                NoOpDaemonLogger.Instance);
            using var cancellationTokenSource = new CancellationTokenSource();
            var responseTask = handler.HandleAsync(
                    CreateRequest(
                        Guid.NewGuid(),
                        IpcMethodNames.GuiRebootstrap,
                        new IpcGuiRebootstrapRequest(
                            ProjectFingerprint: "project-fingerprint",
                            ReplaceExistingSession: true)),
                    cancellationTokenSource.Token)
                .AsTask();
            await TestAwaiter.WaitAsync(
                bootstrapStarter.Started,
                "GUI rebootstrap cancellation test start",
                SignalWaitTimeout);

            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await responseTask.AsUniTask();
            }, "Canceled GUI rebootstrap request", SignalWaitTimeout);
            Assert.That(bootstrapStarter.CancellationToken, Is.EqualTo(cancellationTokenSource.Token));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PlayStatusHandler_WhenPayloadIsValid_ReturnsLifecycleSnapshotWithoutReadinessWait () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new StubUnityEditorReadinessGate(DaemonEditorMode.Gui);
            var handler = new PlayStatusUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                new IpcProjectIdentity("/repo/UnityProject", "project-fingerprint", "6000.1.4f1"),
                NoOpDaemonLogger.Instance);
            var request = CreatePlayStatusRequest(Guid.NewGuid(), new IpcPlayStatusRequest());

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(readinessGate.CaptureSnapshotCallCount, Is.EqualTo(1));
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPlayStatusResponse payload, out _), Is.True);
            Assert.That(payload.Snapshot.ServerVersion, Is.EqualTo("1.2.3"));
            Assert.That(payload.Snapshot.EditorMode, Is.EqualTo("gui"));
            Assert.That(payload.Snapshot.UnityVersion, Is.EqualTo("6000.1.4f1"));
            Assert.That(payload.Snapshot.ProjectFingerprint, Is.EqualTo("project-fingerprint"));
            Assert.That(payload.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(payload.Snapshot.CompileState, Is.EqualTo(IpcCompileStateCodec.Ready));
            Assert.That(payload.Snapshot.PlayMode, Is.Not.Null);
            Assert.That(payload.Snapshot.PlayMode!.State, Is.EqualTo("stopped"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PlayStatusHandler_WhenPayloadIsInvalid_ReturnsInvalidArgumentWithoutCapturingSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new StubUnityEditorReadinessGate(DaemonEditorMode.Gui);
            var handler = new PlayStatusUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                new IpcProjectIdentity("/repo/UnityProject", "project-fingerprint", "6000.1.4f1"),
                NoOpDaemonLogger.Instance);
            var request = CreatePlayStatusRequest(Guid.NewGuid(), 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(readinessGate.CaptureSnapshotCallCount, Is.EqualTo(0));
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteHandler_WhenPayloadIsValid_CallsDispatcher () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = StubExecuteRequestDispatcher.CreateSuccessful();
            var handler = CreateExecuteHandler(dispatcher, new IpcRequestTimeoutScopeFactory());
            var requestId = Guid.NewGuid();
            var request = CreateExecuteRequest(
                requestId,
                new IpcExecuteRequest(
                    UcliCommandIds.Validate,
                    IpcPayloadCodec.SerializeToElement(new
                    {
                        protocolVersion = IpcProtocol.CurrentVersion,
                        requestId = Guid.NewGuid(),
                        ops = Array.Empty<object>(),
                    })));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(dispatcher.CallCount, Is.EqualTo(1));
            Assert.That(dispatcher.LastContext, Is.Not.Null);
            Assert.That(dispatcher.LastContext.RequestId, Is.EqualTo(requestId));
            Assert.That(dispatcher.LastRequest, Is.Not.Null);
            Assert.That(dispatcher.LastRequest.Command, Is.EqualTo(UcliCommandIds.Validate.Name));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateExecuteHandler(StubExecuteRequestDispatcher.CreateSuccessful(), new IpcRequestTimeoutScopeFactory());
            var request = CreateExecuteRequest(Guid.NewGuid(), 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteHandler_WhenTimeoutMillisecondsIsInvalid_ReturnsInvalidArgumentWithoutCallingDispatcher () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = StubExecuteRequestDispatcher.CreateSuccessful();
            var handler = CreateExecuteHandler(dispatcher, new IpcRequestTimeoutScopeFactory());
            var request = CreateExecuteRequest(
                Guid.NewGuid(),
                new IpcExecuteRequest(
                    UcliCommandIds.Call,
                    IpcPayloadCodec.SerializeToElement(new
                    {
                        protocolVersion = IpcProtocol.CurrentVersion,
                        requestId = Guid.NewGuid(),
                        steps = Array.Empty<object>(),
                    }))
                {
                    TimeoutMilliseconds = 0,
                });

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(dispatcher.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteHandler_WhenRequestTimeoutElapses_ReturnsIpcTimeoutAndCancelsDispatcher () => UniTask.ToCoroutine(async () =>
        {
            var timeoutScopeFactory = new ManualIpcRequestTimeoutScopeFactory();
            var dispatcherAwaitReadySource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dispatcherCancellationObservedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dispatcher = new StubExecuteRequestDispatcher(async (_, context, cancellationToken) =>
            {
                try
                {
                    dispatcherAwaitReadySource.TrySetResult(true);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    dispatcherCancellationObservedSource.TrySetResult(true);
                    throw;
                }

                return new IpcResponse(
                    protocolVersion: context.ProtocolVersion,
                    requestId: context.RequestId,
                    status: IpcProtocol.StatusOk,
                    payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>())),
                    errors: Array.Empty<IpcError>());
            });
            var handler = CreateExecuteHandler(dispatcher, timeoutScopeFactory);
            var request = CreateExecuteRequest(
                Guid.NewGuid(),
                new IpcExecuteRequest(
                    UcliCommandIds.Call,
                    IpcPayloadCodec.SerializeToElement(new
                    {
                        protocolVersion = IpcProtocol.CurrentVersion,
                        requestId = Guid.NewGuid(),
                        steps = Array.Empty<object>(),
                    }))
                {
                    TimeoutMilliseconds = 1000,
                });

            var responseTask = handler.HandleAsync(request, CancellationToken.None).AsTask();
            await TestAwaiter.WaitAsync(
                dispatcherAwaitReadySource.Task,
                "execute dispatcher await point",
                SignalWaitTimeout);

            Assert.That(responseTask.IsCompleted, Is.False);

            timeoutScopeFactory.LastScope.CancelForTimeout();
            await TestAwaiter.WaitAsync(dispatcherCancellationObservedSource.Task, "execute request timeout cancellation", SignalWaitTimeout);

            var response = await TestAwaiter.WaitAsync(responseTask, "execute timeout response", SignalWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteHandler_WhenCallerCancellationIsRequested_ThrowsWithoutReturningIpcTimeout () => UniTask.ToCoroutine(async () =>
        {
            var dispatcherAwaitReadySource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dispatcherCancellationObservedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var requestId = Guid.NewGuid();
            var dispatcher = new StubExecuteRequestDispatcher(async (_, _, cancellationToken) =>
            {
                try
                {
                    dispatcherAwaitReadySource.TrySetResult(true);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    dispatcherCancellationObservedSource.TrySetResult(true);
                    throw;
                }

                return new IpcResponse(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: requestId,
                    status: IpcProtocol.StatusOk,
                    payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>())),
                    errors: Array.Empty<IpcError>());
            });
            var handler = CreateExecuteHandler(dispatcher, new IpcRequestTimeoutScopeFactory());
            var request = CreateExecuteRequest(
                requestId,
                new IpcExecuteRequest(
                    UcliCommandIds.Call,
                    IpcPayloadCodec.SerializeToElement(new
                    {
                        protocolVersion = IpcProtocol.CurrentVersion,
                        requestId = Guid.NewGuid(),
                        steps = Array.Empty<object>(),
                    }))
                {
                    TimeoutMilliseconds = 60000,
                });
            using var cancellationTokenSource = new CancellationTokenSource();

            var responseTask = handler.HandleAsync(request, cancellationTokenSource.Token).AsTask();
            await TestAwaiter.WaitAsync(
                dispatcherAwaitReadySource.Task,
                "execute dispatcher await point before caller cancellation",
                SignalWaitTimeout);

            cancellationTokenSource.Cancel();
            await TestAwaiter.WaitAsync(
                dispatcherCancellationObservedSource.Task,
                "execute caller cancellation observation",
                SignalWaitTimeout);

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await TestAwaiter.WaitAsync(
                    responseTask,
                    "execute caller cancellation response",
                    SignalWaitTimeout);
            }, "execute caller cancellation propagation", SignalWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator OpsReadHandler_WhenReady_ReturnsCatalogResponse () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new StubUnityEditorReadinessGate();
            var handler = CreateOpsReadHandler(readinessGate);
            var request = CreateOpsReadRequest(Guid.NewGuid(), new IpcOpsReadRequest());

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcOpsReadResponse payload, out _), Is.True);
            Assert.That(payload.Operations.Count, Is.EqualTo(1));
            Assert.That(payload.Operations[0].Name, Is.EqualTo(UcliPrimitiveOperationNames.GoDescribe));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator OpsReadHandler_WhenEditLoweringCatalogIsRequested_ReturnsValidationCatalog () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new StubUnityEditorReadinessGate();
            var handler = CreateOpsReadHandler(readinessGate);
            var request = CreateOpsReadRequest(Guid.NewGuid(), new IpcOpsReadRequest(IncludeEditLoweringOnly: true));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcOpsReadResponse payload, out _), Is.True);
            Assert.That(payload.Operations.Select(static operation => operation.Name), Does.Contain(UcliPrimitiveOperationNames.AssetSave));
            var assetSave = payload.Operations.Single(static operation => operation.Name == UcliPrimitiveOperationNames.AssetSave);
            Assert.That(assetSave.Exposure, Is.EqualTo("editLoweringOnly"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator OpsReadHandler_WhenFailFastIsDisabled_DelaysResponseUntilReady () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var handler = CreateOpsReadHandler(readinessGate);
            var responseTask = handler.HandleAsync(
                CreateOpsReadRequest(Guid.NewGuid(), new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true)),
                CancellationToken.None).AsTask();

            await TestAwaiter.WaitAsync(readinessGate.WaitObserved, "ops.read readiness wait", SignalWaitTimeout);

            Assert.That(readinessGate.LastFailFast, Is.False);

            readinessGate.Release();

            var response = await TestAwaiter.WaitAsync(responseTask, "ops.read response after readiness", SignalWaitTimeout);
            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator OpsReadHandler_WhenFailFastIsEnabled_ReturnsLifecycleFailure () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var handler = CreateOpsReadHandler(readinessGate);
            var request = CreateOpsReadRequest(
                Guid.NewGuid(),
                new IpcOpsReadRequest(FailFast: true, RequireReadinessGate: true));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(readinessGate.LastFailFast, Is.True);
            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenServiceSucceeds_ReturnsOkResponse () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService(request => Task.FromResult(UnityTestRunServiceResult.Success(new IpcTestRunResponse(2))));
            var handler = CreateTestRunHandler(service, new IpcRequestTimeoutScopeFactory());
            var request = CreateTestRunRequest(
                Guid.NewGuid(),
                CreateValidTestRunPayload(failFast: true));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(service.CallCount, Is.EqualTo(1));
            Assert.That(service.LastRequest, Is.Not.Null);
            Assert.That(service.LastRequest.FailFast, Is.True);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcTestRunResponse payload, out _), Is.True);
            Assert.That(payload.ExitCode, Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenStreaming_ForwardsAcceptedProgressAndIgnoresProgressAfterCompletion () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService((request, progressSink) =>
            {
                progressSink.Publish(
                    TestRunProgressEventNames.CaseStarted,
                    new TestCaseStartedEntry(
                        request.RunId,
                        "test-id",
                        "SampleTest",
                        "Assembly-CSharp-Editor",
                        TestRunPlatformCodec.EditMode,
                        new[] { "fast" }));
                progressSink.Publish(
                    TestRunProgressEventNames.CaseFinished,
                    new TestCaseFinishedEntry(
                        request.RunId,
                        "test-id",
                        "SampleTest",
                        "Assembly-CSharp-Editor",
                        TestRunPlatformCodec.EditMode,
                        new[] { "fast" },
                        "pass",
                        42,
                        null,
                        null));
                return Task.FromResult(UnityTestRunServiceResult.Success(new IpcTestRunResponse(0)));
            });
            var handler = CreateTestRunHandler(service, new IpcRequestTimeoutScopeFactory());
            var requestId = Guid.NewGuid();
            var streamWriter = new CollectingIpcStreamFrameWriter(requestId);
            var request = CreateTestRunRequest(
                requestId,
                CreateValidTestRunPayload());

            var response = await handler.HandleStreamingAsync(request, streamWriter, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(service.CallCount, Is.EqualTo(1));
            Assert.That(service.LastProgressSink, Is.Not.Null);
            Assert.That(streamWriter.ProgressFrames.Count, Is.EqualTo(2));
            Assert.That(streamWriter.ProgressFrames[0].Event, Is.EqualTo(TestRunProgressEventNames.CaseStarted));
            Assert.That(streamWriter.ProgressFrames[1].Event, Is.EqualTo(TestRunProgressEventNames.CaseFinished));
            Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[0].Payload, out TestCaseStartedEntry started, out _), Is.True);
            Assert.That(started.RunId, Is.EqualTo("run-id"));
            Assert.That(started.TestName, Is.EqualTo("SampleTest"));
            Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[1].Payload, out TestCaseFinishedEntry finished, out _), Is.True);
            Assert.That(finished.RunId, Is.EqualTo("run-id"));
            Assert.That(finished.Result, Is.EqualTo("pass"));

            service.LastProgressSink.Publish(
                TestRunProgressEventNames.CaseStarted,
                new TestCaseStartedEntry(
                    "run-id",
                    "late-test-id",
                    "Late test",
                    "Assembly-CSharp-Editor",
                    TestRunPlatformCodec.EditMode,
                    Array.Empty<string>()));

            Assert.That(streamWriter.ProgressFrames.Count, Is.EqualTo(2));
            Assert.That(streamWriter.ProgressWriteAttemptCount, Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenProgressFlushFails_ReturnsInternalErrorWithoutReflushingFaultedSink () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService((request, progressSink) =>
            {
                progressSink.Publish(
                    TestRunProgressEventNames.CaseStarted,
                    new TestCaseStartedEntry(
                        request.RunId,
                        "test-id",
                        "SampleTest",
                        "Assembly-CSharp-Editor",
                        TestRunPlatformCodec.EditMode,
                        Array.Empty<string>()));
                return Task.FromResult(UnityTestRunServiceResult.Success(new IpcTestRunResponse(0)));
            });
            var handler = CreateTestRunHandler(service, new IpcRequestTimeoutScopeFactory());
            var requestId = Guid.NewGuid();
            var streamWriter = new CollectingIpcStreamFrameWriter(
                requestId,
                new IOException("progress write failed"));
            var request = CreateTestRunRequest(
                requestId,
                CreateValidTestRunPayload());

            var response = await handler.HandleStreamingAsync(request, streamWriter, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(response.Errors[0].Message, Does.Contain("progress write failed"));
            Assert.That(streamWriter.ProgressWriteAttemptCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenRequestTimeoutElapses_ReturnsIpcTimeoutAndCancelsService () => UniTask.ToCoroutine(async () =>
        {
            var timeoutScopeFactory = new ManualIpcRequestTimeoutScopeFactory();
            var serviceAwaitReadySource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var serviceCancellationObservedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var service = new StubUnityTestRunService(async (request, _, cancellationToken) =>
            {
                try
                {
                    serviceAwaitReadySource.TrySetResult(true);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    serviceCancellationObservedSource.TrySetResult(true);
                    throw;
                }

                return UnityTestRunServiceResult.Success(new IpcTestRunResponse(0));
            });
            var handler = CreateTestRunHandler(service, timeoutScopeFactory);
            var request = CreateTestRunRequest(
                Guid.NewGuid(),
                CreateValidTestRunPayload(timeoutMilliseconds: 1000));

            var responseTask = handler.HandleAsync(request, CancellationToken.None).AsTask();
            await TestAwaiter.WaitAsync(
                serviceAwaitReadySource.Task,
                "test-run service await point",
                SignalWaitTimeout);

            Assert.That(responseTask.IsCompleted, Is.False);

            timeoutScopeFactory.LastScope.CancelForTimeout();
            await TestAwaiter.WaitAsync(serviceCancellationObservedSource.Task, "test-run request timeout cancellation", SignalWaitTimeout);

            var response = await TestAwaiter.WaitAsync(responseTask, "test-run timeout response", SignalWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenStreamingRequestTimeoutElapsesWithPendingProgress_StopsWaitingAndReturnsIpcTimeout () => UniTask.ToCoroutine(async () =>
        {
            var timeoutScopeFactory = new ManualIpcRequestTimeoutScopeFactory();
            var serviceAwaitReadySource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var serviceCancellationObservedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var service = new StubUnityTestRunService(async (request, progressSink, cancellationToken) =>
            {
                progressSink.Publish(
                    TestRunProgressEventNames.CaseStarted,
                    new TestCaseStartedEntry(
                        request.RunId,
                        "test-id",
                        "SampleTest",
                        "Assembly-CSharp-Editor",
                        TestRunPlatformCodec.EditMode,
                        Array.Empty<string>()));

                try
                {
                    serviceAwaitReadySource.TrySetResult(true);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    serviceCancellationObservedSource.TrySetResult(true);
                    throw;
                }

                return UnityTestRunServiceResult.Success(new IpcTestRunResponse(0));
            });
            var handler = CreateTestRunHandler(service, timeoutScopeFactory);
            var requestId = Guid.NewGuid();
            var streamWriter = new BlockingIpcStreamFrameWriter(requestId);
            var request = CreateTestRunRequest(
                requestId,
                CreateValidTestRunPayload(timeoutMilliseconds: 1000));

            var responseTask = handler.HandleStreamingAsync(request, streamWriter, CancellationToken.None).AsTask();
            await TestAwaiter.WaitAsync(streamWriter.FirstWriteObserved, "first blocked progress write", SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                serviceAwaitReadySource.Task,
                "streaming test-run service await point",
                SignalWaitTimeout);
            timeoutScopeFactory.LastScope.CancelForTimeout();
            await TestAwaiter.WaitAsync(serviceCancellationObservedSource.Task, "streaming test-run request timeout", SignalWaitTimeout);

            try
            {
                Assert.That(streamWriter.LastWriteCancellationToken.IsCancellationRequested, Is.True);
                var response = await TestAwaiter.WaitAsync(responseTask, "streaming test-run timeout response", SignalWaitTimeout);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
                Assert.That(streamWriter.ProgressFrames.Count, Is.EqualTo(1));
            }
            finally
            {
                streamWriter.ReleaseWrites();
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenTimeoutElapsesDuringProgressCompletion_ReturnsWithoutWaitingForNonCooperativeWrite () => UniTask.ToCoroutine(async () =>
        {
            var timeoutScopeFactory = new ManualIpcRequestTimeoutScopeFactory();
            var service = new StubUnityTestRunService((request, progressSink) =>
            {
                progressSink.Publish(
                    TestRunProgressEventNames.CaseStarted,
                    new TestCaseStartedEntry(
                        request.RunId,
                        "test-id",
                        "SampleTest",
                        "Assembly-CSharp-Editor",
                        TestRunPlatformCodec.EditMode,
                        Array.Empty<string>()));
                return Task.FromResult(UnityTestRunServiceResult.Success(new IpcTestRunResponse(0)));
            });
            var handler = CreateTestRunHandler(service, timeoutScopeFactory);
            var requestId = Guid.NewGuid();
            var streamWriter = new BlockingIpcStreamFrameWriter(
                requestId);
            var request = CreateTestRunRequest(
                requestId,
                CreateValidTestRunPayload(timeoutMilliseconds: 1000));

            var responseTask = handler.HandleStreamingAsync(request, streamWriter, CancellationToken.None).AsTask();
            await TestAwaiter.WaitAsync(
                streamWriter.FirstWriteObserved,
                "non-cooperative test-run progress write",
                SignalWaitTimeout);

            Assert.That(responseTask.IsCompleted, Is.False);

            timeoutScopeFactory.LastScope.CancelForTimeout();
            try
            {
                var response = await TestAwaiter.WaitAsync(
                    responseTask,
                    "test-run progress completion timeout response",
                    SignalWaitTimeout);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
                Assert.That(streamWriter.LastWriteCancellationToken.IsCancellationRequested, Is.True);
            }
            finally
            {
                streamWriter.ReleaseWrites();
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunProgressSink_WhenPendingFrameLimitIsExceeded_QueuesOneDropDiagnosticAndCompletionWaits () => UniTask.ToCoroutine(async () =>
        {
            var streamWriter = new BlockingIpcStreamFrameWriter(Guid.NewGuid());
            var progressSink = new UnityIpcTestRunProgressSink(
                streamWriter,
                "run-id",
                CancellationToken.None);

            for (var i = 0; i < 1026; i++)
            {
                progressSink.Publish(
                    TestRunProgressEventNames.CaseStarted,
                    new TestCaseStartedEntry(
                        "run-id",
                        $"test-{i}",
                        $"Test {i}",
                        "Assembly-CSharp-Editor",
                        TestRunPlatformCodec.EditMode,
                        Array.Empty<string>()));
            }

            var completionTask = progressSink.CompleteAndFlushAsync(CancellationToken.None);

            Assert.That(completionTask.IsCompleted, Is.False);

            streamWriter.ReleaseWrites();
            await completionTask;

            Assert.That(streamWriter.ProgressFrames.Count, Is.EqualTo(1025));
            Assert.That(streamWriter.ProgressFrames[0].Event, Is.EqualTo(TestRunProgressEventNames.CaseStarted));
            Assert.That(streamWriter.ProgressFrames[1023].Event, Is.EqualTo(TestRunProgressEventNames.CaseStarted));
            Assert.That(streamWriter.ProgressFrames[1024].Event, Is.EqualTo(TestRunProgressEventNames.RunDiagnostic));
            Assert.That(IpcPayloadCodec.TryDeserialize(streamWriter.ProgressFrames[1024].Payload, out TestRunDiagnosticEntry diagnostic, out _), Is.True);
            Assert.That(diagnostic.Code, Is.EqualTo("TEST_PROGRESS_DROPPED"));
            Assert.That(diagnostic.Severity, Is.EqualTo("warning"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunProgressSink_WhenCompletionStarts_FlushesAcceptedFrameAndIgnoresLaterPublish () => UniTask.ToCoroutine(async () =>
        {
            var streamWriter = new BlockingIpcStreamFrameWriter(Guid.NewGuid());
            var progressSink = new UnityIpcTestRunProgressSink(
                streamWriter,
                "run-id",
                CancellationToken.None);
            progressSink.Publish(
                TestRunProgressEventNames.CaseStarted,
                new TestCaseStartedEntry(
                    "run-id",
                    "accepted-test-id",
                    "Accepted test",
                    "Assembly-CSharp-Editor",
                    TestRunPlatformCodec.EditMode,
                    Array.Empty<string>()));
            await TestAwaiter.WaitAsync(
                streamWriter.FirstWriteObserved,
                "accepted test-run progress write",
                SignalWaitTimeout);

            var completionTask = progressSink.CompleteAndFlushAsync(CancellationToken.None);
            progressSink.Publish(
                TestRunProgressEventNames.CaseStarted,
                new TestCaseStartedEntry(
                    "run-id",
                    "late-test-id",
                    "Late test",
                    "Assembly-CSharp-Editor",
                    TestRunPlatformCodec.EditMode,
                    Array.Empty<string>()));

            Assert.That(completionTask.IsCompleted, Is.False);

            streamWriter.ReleaseWrites();
            await TestAwaiter.WaitAsync(
                completionTask,
                "test-run progress completion",
                SignalWaitTimeout);

            Assert.DoesNotThrow(() => progressSink.Publish(string.Empty, null));
            Assert.That(streamWriter.ProgressFrames.Count, Is.EqualTo(1));
            Assert.That(streamWriter.ProgressFrames[0].Event, Is.EqualTo(TestRunProgressEventNames.CaseStarted));
            Assert.That(
                streamWriter.ProgressFrames[0].Payload.GetProperty("testId").GetString(),
                Is.EqualTo("accepted-test-id"));
        });

        [Test]
        [Category("Size.Small")]
        public void TestRunProgressSink_WhenAcceptanceIsCanceled_IgnoresLateUnityCallbacks ()
        {
            var streamWriter = new CollectingIpcStreamFrameWriter(Guid.NewGuid());
            using var cancellationTokenSource = new CancellationTokenSource();
            var progressSink = new UnityIpcTestRunProgressSink(
                streamWriter,
                "run-id",
                cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();

            Assert.DoesNotThrow(() => progressSink.Publish(
                TestRunProgressEventNames.CaseStarted,
                new TestCaseStartedEntry(
                    "run-id",
                    "test-id",
                    "Late test",
                    "Assembly-CSharp-Editor",
                    TestRunPlatformCodec.EditMode,
                    Array.Empty<string>())));

            Assert.That(streamWriter.ProgressFrames, Is.Empty);
            Assert.That(streamWriter.ProgressWriteAttemptCount, Is.Zero);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenServiceReturnsLifecycleFailure_PreservesErrorCode () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService(_ => Task.FromResult(UnityTestRunServiceResult.Failure(
                new IpcError(EditorLifecycleErrorCodes.EditorBusy, "Unity editor is busy with internal work.", null))));
            var handler = CreateTestRunHandler(service, new IpcRequestTimeoutScopeFactory());
            var request = CreateTestRunRequest(
                Guid.NewGuid(),
                CreateValidTestRunPayload());

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
            Assert.That(response.Errors[0].Message, Is.EqualTo("Unity editor is busy with internal work."));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenServiceThrowsArgumentException_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService(_ => throw new ArgumentException("invalid"));
            var handler = CreateTestRunHandler(service, new IpcRequestTimeoutScopeFactory());
            var request = CreateTestRunRequest(
                Guid.NewGuid(),
                CreateValidTestRunPayload());

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenServiceThrowsUnexpectedException_ReturnsInternalError () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService(_ => throw new InvalidOperationException("test-run-failed"));
            var handler = CreateTestRunHandler(service, new IpcRequestTimeoutScopeFactory());
            var request = CreateTestRunRequest(
                Guid.NewGuid(),
                CreateValidTestRunPayload());

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(response.Errors[0].Message, Does.Contain("test-run-failed"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateTestRunHandler(
                new StubUnityTestRunService(request => Task.FromResult(UnityTestRunServiceResult.Success(new IpcTestRunResponse(0)))),
                new IpcRequestTimeoutScopeFactory());
            var request = CreateTestRunRequest(Guid.NewGuid(), 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator IndexAssetsReadHandler_WhenPayloadIsValid_ReturnsOkResponse () => UniTask.ToCoroutine(async () =>
        {
            var handler = new IndexAssetsReadUnityIpcMethodHandler(
                new StubAssetLookupSnapshotBuilder(
                    () => new IpcIndexAssetsReadResponse(
                        GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                        AssetSearchEntries: new[]
                        {
                            new MackySoft.Ucli.Contracts.Index.IndexAssetSearchEntryJsonContract(
                                AssetPath: "Assets/Data/Spawner.asset",
                                AssetGuid: "11111111111111111111111111111111",
                                Name: "Spawner",
                                TypeId: "Game.Spawner, Assembly-CSharp",
                                SearchTypeIds: new[]
                                {
                                    "Game.Spawner, Assembly-CSharp",
                                    "UnityEngine.Object, UnityEngine.CoreModule",
                                }),
                        },
                        GuidPathEntries: new[]
                        {
                            new MackySoft.Ucli.Contracts.Index.IndexGuidPathEntryJsonContract(
                                AssetGuid: "11111111111111111111111111111111",
                                AssetPath: "Assets/Data/Spawner.asset"),
                        })),
                new StubUnityEditorReadinessGate());
            var request = CreateIndexAssetsReadRequest(Guid.NewGuid(), new IpcIndexAssetsReadRequest());

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcIndexAssetsReadResponse payload, out _), Is.True);
            Assert.That(payload.AssetSearchEntries, Has.Count.EqualTo(1));
            Assert.That(payload.GuidPathEntries, Has.Count.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator IndexAssetsReadHandler_WhenFailFastAndEditorIsBusy_ReturnsReadinessErrorWithoutBuildingSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var builder = new StubAssetLookupSnapshotBuilder(
                () => new IpcIndexAssetsReadResponse(
                    GeneratedAtUtc: DateTimeOffset.UtcNow,
                    AssetSearchEntries: Array.Empty<MackySoft.Ucli.Contracts.Index.IndexAssetSearchEntryJsonContract>(),
                    GuidPathEntries: Array.Empty<MackySoft.Ucli.Contracts.Index.IndexGuidPathEntryJsonContract>()));
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var handler = new IndexAssetsReadUnityIpcMethodHandler(builder, readinessGate);
            var request = CreateIndexAssetsReadRequest(
                Guid.NewGuid(),
                new IpcIndexAssetsReadRequest(FailFast: true));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.True);
            Assert.That(builder.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator IndexAssetsReadHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = new IndexAssetsReadUnityIpcMethodHandler(
                new StubAssetLookupSnapshotBuilder(
                    () => new IpcIndexAssetsReadResponse(
                        GeneratedAtUtc: DateTimeOffset.UtcNow,
                        AssetSearchEntries: Array.Empty<MackySoft.Ucli.Contracts.Index.IndexAssetSearchEntryJsonContract>(),
                        GuidPathEntries: Array.Empty<MackySoft.Ucli.Contracts.Index.IndexGuidPathEntryJsonContract>())),
                new StubUnityEditorReadinessGate());
            var request = CreateIndexAssetsReadRequest(Guid.NewGuid(), 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator IndexSceneTreeLiteReadHandler_WhenPayloadIsValid_ReturnsOkResponse () => UniTask.ToCoroutine(async () =>
        {
            var handler = new IndexSceneTreeLiteReadUnityIpcMethodHandler(
                new StubSceneTreeLiteSnapshotBuilder(
                    scenePath => CreateIndexSceneTreeLiteReadResponse(scenePath, "Root")),
                new StubUnityEditorReadinessGate());
            var request = CreateIndexSceneTreeLiteReadRequest(
                Guid.NewGuid(),
                new IpcIndexSceneTreeLiteReadRequest("Assets/Scenes/Main.unity"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcIndexSceneTreeLiteReadResponse payload, out _), Is.True);
            Assert.That(payload.ScenePath, Is.EqualTo("Assets/Scenes/Main.unity"));
            Assert.That(payload.Roots, Has.Count.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator IndexSceneTreeLiteReadHandler_WhenLoadedSceneOnlyIsSpecified_PassesFlagToBuilder () => UniTask.ToCoroutine(async () =>
        {
            var builder = new StubSceneTreeLiteSnapshotBuilder(
                scenePath => CreateIndexSceneTreeLiteReadResponse(scenePath, "Root"));
            var handler = new IndexSceneTreeLiteReadUnityIpcMethodHandler(builder, new StubUnityEditorReadinessGate());
            var request = CreateIndexSceneTreeLiteReadRequest(
                Guid.NewGuid(),
                new IpcIndexSceneTreeLiteReadRequest(
                    "Assets/Scenes/Main.unity",
                    LoadedSceneOnly: true));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(builder.LastLoadedSceneOnly, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator IndexSceneTreeLiteReadHandler_WhenFailFastAndEditorIsBusy_ReturnsReadinessErrorWithoutBuildingSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var builder = new StubSceneTreeLiteSnapshotBuilder(
                scenePath => CreateIndexSceneTreeLiteReadResponse(scenePath, "Root"));
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var handler = new IndexSceneTreeLiteReadUnityIpcMethodHandler(builder, readinessGate);
            var request = CreateIndexSceneTreeLiteReadRequest(
                Guid.NewGuid(),
                new IpcIndexSceneTreeLiteReadRequest("Assets/Scenes/Main.unity", FailFast: true));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.True);
            Assert.That(builder.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator IndexSceneTreeLiteReadHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = new IndexSceneTreeLiteReadUnityIpcMethodHandler(
                new StubSceneTreeLiteSnapshotBuilder(
                    scenePath => CreateIndexSceneTreeLiteReadResponse(scenePath, "Root")),
                new StubUnityEditorReadinessGate());
            var request = CreateIndexSceneTreeLiteReadRequest(Guid.NewGuid(), 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator IndexSceneTreeLiteReadHandler_WhenBuilderThrows_ReturnsInternalError () => UniTask.ToCoroutine(async () =>
        {
            var handler = new IndexSceneTreeLiteReadUnityIpcMethodHandler(
                new StubSceneTreeLiteSnapshotBuilder(
                    scenePath => throw new InvalidOperationException("scene-tree-lite-failed")),
                new StubUnityEditorReadinessGate());
            var request = CreateIndexSceneTreeLiteReadRequest(
                Guid.NewGuid(),
                new IpcIndexSceneTreeLiteReadRequest("Assets/Scenes/Main.unity"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(response.Errors[0].Message, Does.Contain("scene-tree-lite-failed"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ShutdownHandler_WhenPayloadIsValid_ReturnsAcceptedResponse () => UniTask.ToCoroutine(async () =>
        {
            using var mutationExecutor = CreateMutationExecutor();
            using var shutdownAdmission = new UnityShutdownAdmissionCoordinator(mutationExecutor);
            var handler = new ShutdownUnityIpcMethodHandler(
                NoOpDaemonLogger.Instance,
                shutdownAdmission);
            var request = CreateShutdownRequest(Guid.NewGuid(), new IpcShutdownRequest("tests"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcShutdownResponse payload, out _), Is.True);
            Assert.That(payload.Accepted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ShutdownHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            using var mutationExecutor = CreateMutationExecutor();
            using var shutdownAdmission = new UnityShutdownAdmissionCoordinator(mutationExecutor);
            var handler = new ShutdownUnityIpcMethodHandler(
                NoOpDaemonLogger.Instance,
                shutdownAdmission);
            var request = CreateShutdownRequest(Guid.NewGuid(), 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ShutdownHandler_WhenMutationIsActive_ReturnsEditorBusyWithoutStoppingAdmission () => UniTask.ToCoroutine(async () =>
        {
            using var mutationExecutor = new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations,
                poisonOnActiveCancellation: true);
            var release = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var activeMutation = mutationExecutor.ExecuteAsync(async () =>
            {
                await release.Task;
                return true;
            });
            using var shutdownAdmission = new UnityShutdownAdmissionCoordinator(mutationExecutor);
            var handler = new ShutdownUnityIpcMethodHandler(
                NoOpDaemonLogger.Instance,
                shutdownAdmission);
            var request = CreateShutdownRequest(Guid.NewGuid(), new IpcShutdownRequest("tests"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
            Assert.That(mutationExecutor.IsPoisoned, Is.False);

            release.TrySetResult(null);
            await TestAwaiter.WaitAsync(activeMutation.AsUniTask(), "Active mutation completion", TimeSpan.FromSeconds(5));
        });

        [Test]
        [Category("Size.Small")]
        public void ShutdownAdmission_WhenAnotherExchangeAborts_PreservesOwningExchangeSeal ()
        {
            using var mutationExecutor = new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations,
                poisonOnActiveCancellation: true);
            using var shutdownAdmission = new UnityShutdownAdmissionCoordinator(mutationExecutor);
            var owningRequest = CreateShutdownRequest(Guid.NewGuid(), new IpcShutdownRequest("tests"));
            var competingRequest = CreateShutdownRequest(Guid.NewGuid(), new IpcShutdownRequest("tests"));

            Assert.That(shutdownAdmission.TryPrepare(owningRequest, out _), Is.True);
            Assert.That(shutdownAdmission.TryPrepare(competingRequest, out _), Is.False);

            shutdownAdmission.Abort(competingRequest);

            Assert.That(mutationExecutor.IsBusy, Is.True);
            Assert.That(shutdownAdmission.TryCommit(owningRequest), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void ShutdownAdmission_WhenCommittedRequestIsResentWithSameId_AcceptsResentExchange ()
        {
            using var mutationExecutor = CreateMutationExecutor();
            using var shutdownAdmission = new UnityShutdownAdmissionCoordinator(mutationExecutor);
            var requestId = Guid.NewGuid();
            var firstExchange = CreateShutdownRequest(requestId, new IpcShutdownRequest("tests"));
            var resentExchange = CreateShutdownRequest(requestId, new IpcShutdownRequest("tests"));

            Assert.That(shutdownAdmission.TryPrepare(firstExchange, out _), Is.True);
            Assert.That(shutdownAdmission.TryCommit(firstExchange), Is.True);

            Assert.That(shutdownAdmission.TryPrepare(resentExchange, out _), Is.True);
            Assert.That(shutdownAdmission.TryCommit(resentExchange), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void ShutdownAdmission_WhenOneSameIdExchangeAborts_PreservesOtherExchangeSeal ()
        {
            using var mutationExecutor = CreateMutationExecutor();
            using var shutdownAdmission = new UnityShutdownAdmissionCoordinator(mutationExecutor);
            var requestId = Guid.NewGuid();
            var firstExchange = CreateShutdownRequest(requestId, new IpcShutdownRequest("tests"));
            var resentExchange = CreateShutdownRequest(requestId, new IpcShutdownRequest("tests"));

            Assert.That(shutdownAdmission.TryPrepare(firstExchange, out _), Is.True);
            Assert.That(shutdownAdmission.TryPrepare(resentExchange, out _), Is.True);

            shutdownAdmission.Abort(firstExchange);

            Assert.That(mutationExecutor.IsBusy, Is.True);
            Assert.That(shutdownAdmission.TryCommit(resentExchange), Is.True);
        }

        private static UnitySynchronizationContextRequestExecutor CreateMutationExecutor ()
        {
            return new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations,
                poisonOnActiveCancellation: true);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityConsoleClearHandler_WhenPayloadIsValidAndEditorModeIsGui_CallsClearerAndReturnsOk () => UniTask.ToCoroutine(async () =>
        {
            var clearer = new StubUnityConsoleClearer(UnityConsoleClearResult.Success());
            var handler = CreateUnityConsoleClearHandler(clearer, DaemonEditorMode.Gui);
            var request = CreateUnityConsoleClearRequest(
                Guid.NewGuid(),
                new IpcUnityConsoleClearRequest("tests"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(clearer.CallCount, Is.EqualTo(1));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityConsoleClearResponse payload, out _), Is.True);
            Assert.That(payload, Is.Not.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityConsoleClearHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateUnityConsoleClearHandler(new StubUnityConsoleClearer(UnityConsoleClearResult.Success()), DaemonEditorMode.Gui);
            var request = CreateUnityConsoleClearRequest(Guid.NewGuid(), 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityConsoleClearHandler_WhenRequestedByIsEmpty_ReturnsInvalidArgumentWithoutCallingClearer () => UniTask.ToCoroutine(async () =>
        {
            var clearer = new StubUnityConsoleClearer(UnityConsoleClearResult.Success());
            var handler = CreateUnityConsoleClearHandler(clearer, DaemonEditorMode.Gui);
            var request = CreateUnityConsoleClearRequest(
                Guid.NewGuid(),
                new IpcUnityConsoleClearRequest(" "));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(clearer.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityConsoleClearHandler_WhenRequestedByContainsInvalidCharacter_ReturnsInvalidArgumentWithoutCallingClearer () => UniTask.ToCoroutine(async () =>
        {
            var clearer = new StubUnityConsoleClearer(UnityConsoleClearResult.Success());
            var handler = CreateUnityConsoleClearHandler(clearer, DaemonEditorMode.Gui);
            var request = CreateUnityConsoleClearRequest(
                Guid.NewGuid(),
                new IpcUnityConsoleClearRequest("logs.unity.clear\n"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(clearer.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityConsoleClearHandler_WhenRequestedByIsTooLong_ReturnsInvalidArgumentWithoutCallingClearer () => UniTask.ToCoroutine(async () =>
        {
            var clearer = new StubUnityConsoleClearer(UnityConsoleClearResult.Success());
            var handler = CreateUnityConsoleClearHandler(clearer, DaemonEditorMode.Gui);
            var request = CreateUnityConsoleClearRequest(
                Guid.NewGuid(),
                new IpcUnityConsoleClearRequest(new string('a', 65)));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(clearer.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityConsoleClearHandler_WhenEditorModeIsBatchmode_ReturnsInvalidArgumentWithoutCallingClearer () => UniTask.ToCoroutine(async () =>
        {
            var clearer = new StubUnityConsoleClearer(UnityConsoleClearResult.Success());
            var handler = CreateUnityConsoleClearHandler(clearer, DaemonEditorMode.Batchmode);
            var request = CreateUnityConsoleClearRequest(
                Guid.NewGuid(),
                new IpcUnityConsoleClearRequest("tests"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(clearer.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityConsoleClearHandler_WhenClearerFails_ReturnsInternalError () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateUnityConsoleClearHandler(
                new StubUnityConsoleClearer(UnityConsoleClearResult.Failure("UnityEditor.LogEntries.Clear could not be resolved.")),
                DaemonEditorMode.Gui);
            var request = CreateUnityConsoleClearRequest(
                Guid.NewGuid(),
                new IpcUnityConsoleClearRequest("tests"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
        });

        [Test]
        [Category("Size.Small")]
        public void UnityEditorConsoleClearer_WhenClearMethodResolutionFails_ReturnsFailure ()
        {
            var clearer = new UnityEditorConsoleClearer(null, "reflection resolution failed");

            var result = clearer.Clear();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("reflection resolution failed"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DaemonLogsReadHandler_WhenPayloadIsValid_ReturnsFilteredEventsAndNextCursor () => UniTask.ToCoroutine(async () =>
        {
            var stream = new DaemonLogRingBuffer();
            stream.Write("ipc", "info", "server started");
            stream.Write("transport", "warning", "socket timeout detected", "SocketException");
            var snapshot = stream.Snapshot();
            var firstEventCursor = snapshot.Events[0].Cursor;
            var handler = CreateDaemonLogsReadHandler(stream);
            var request = CreateDaemonLogsReadRequest(
                Guid.NewGuid(),
                new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: firstEventCursor,
                    Since: null,
                    Until: null,
                    Level: "warning",
                    Query: "socket",
                    QueryTarget: "both",
                    Category: "transport"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcDaemonLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(1));
            Assert.That(payload.Events[0].Category, Is.EqualTo("transport"));
            Assert.That(payload.Events[0].Level, Is.EqualTo("warning"));
            Assert.That(payload.NextCursor, Does.StartWith(snapshot.StreamId + ":"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DaemonLogsReadHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateDaemonLogsReadHandler(new DaemonLogRingBuffer());
            var request = CreateDaemonLogsReadRequest(Guid.NewGuid(), 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DaemonLogsReadHandler_WhenQueryTargetIsStack_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var stream = new DaemonLogRingBuffer();
            stream.Write("ipc", "info", "server started");
            var handler = CreateDaemonLogsReadHandler(stream);
            var request = CreateDaemonLogsReadRequest(
                Guid.NewGuid(),
                new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: "socket",
                    QueryTarget: "stack",
                    Category: null));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DaemonLogsReadHandler_WhenCategoryIsAll_DoesNotFilterByCategory () => UniTask.ToCoroutine(async () =>
        {
            var stream = new DaemonLogRingBuffer();
            stream.Write("ipc", "info", "server started");
            stream.Write("transport", "warning", "socket timeout detected");
            var handler = CreateDaemonLogsReadHandler(stream);
            var request = CreateDaemonLogsReadRequest(
                Guid.NewGuid(),
                new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Category: "all"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcDaemonLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator DaemonLogsReadHandler_WhenAfterAndSinceSpecified_AfterHasPriority () => UniTask.ToCoroutine(async () =>
        {
            var stream = new DaemonLogRingBuffer();
            stream.Write("ipc", "info", "before");
            stream.Write("transport", "info", "after");
            var snapshot = stream.Snapshot();
            var secondCursor = snapshot.Events[1].Cursor;
            var since = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
            var handler = CreateDaemonLogsReadHandler(stream);
            var request = CreateDaemonLogsReadRequest(
                Guid.NewGuid(),
                new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: secondCursor,
                    Since: since,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Category: null));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcDaemonLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(1));
            Assert.That(payload.Events[0].Message, Is.EqualTo("after"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenPayloadIsValid_ReturnsFilteredEventsAndNextCursor () => UniTask.ToCoroutine(async () =>
        {
            var stream = new UnityLogRingBuffer();
            stream.Write(IpcUnityLogsSourceCodec.Runtime, IpcDaemonLogsLevelCodec.Info, "player joined");
            stream.Write(IpcUnityLogsSourceCodec.Compile, IpcDaemonLogsLevelCodec.Warning, "Assets/Test.cs(1,2): warning CS0168");
            var snapshot = stream.Snapshot();
            var handler = CreateUnityLogsReadHandler(stream);
            var request = CreateUnityLogsReadRequest(
                Guid.NewGuid(),
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: "info",
                    Query: "player",
                    QueryTarget: "message",
                    Source: "runtime",
                    StackTrace: "all",
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(1));
            Assert.That(payload.Events[0].Source, Is.EqualTo("runtime"));
            Assert.That(payload.Events[0].Message, Is.EqualTo("player joined"));
            Assert.That(payload.NextCursor, Is.EqualTo(snapshot.NextCursor));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateUnityLogsReadHandler(new UnityLogRingBuffer());
            var request = CreateUnityLogsReadRequest(Guid.NewGuid(), 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenAfterCursorIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateUnityLogsReadHandler(new UnityLogRingBuffer());
            var request = CreateUnityLogsReadRequest(
                Guid.NewGuid(),
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: "invalid-cursor",
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Source: null,
                    StackTrace: null,
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenSourceIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateUnityLogsReadHandler(new UnityLogRingBuffer());
            var request = CreateUnityLogsReadRequest(
                Guid.NewGuid(),
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Source: "editor",
                    StackTrace: null,
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenStackTraceModeIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = CreateUnityLogsReadHandler(new UnityLogRingBuffer());
            var request = CreateUnityLogsReadRequest(
                Guid.NewGuid(),
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Source: null,
                    StackTrace: "warningsOnly",
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenQueryTargetIsStack_ReturnsStackMatches () => UniTask.ToCoroutine(async () =>
        {
            var stream = new UnityLogRingBuffer();
            stream.Write(IpcUnityLogsSourceCodec.Runtime, IpcDaemonLogsLevelCodec.Error, "runtime error", "SocketException: broken pipe");
            var handler = CreateUnityLogsReadHandler(stream);
            var request = CreateUnityLogsReadRequest(
                Guid.NewGuid(),
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: "SocketException",
                    QueryTarget: "stack",
                    Source: null,
                    StackTrace: "all",
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(1));
            Assert.That(payload.Events[0].StackTrace, Does.Contain("SocketException"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityLogsReadHandler_WhenAfterAndSinceSpecified_AfterHasPriority () => UniTask.ToCoroutine(async () =>
        {
            var stream = new UnityLogRingBuffer();
            stream.Write(IpcUnityLogsSourceCodec.Runtime, IpcDaemonLogsLevelCodec.Info, "before");
            stream.Write(IpcUnityLogsSourceCodec.Compile, IpcDaemonLogsLevelCodec.Warning, "after");
            var snapshot = stream.Snapshot();
            var secondCursor = snapshot.Events[1].Cursor;
            var since = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
            var handler = CreateUnityLogsReadHandler(stream);
            var request = CreateUnityLogsReadRequest(
                Guid.NewGuid(),
                new IpcUnityLogsReadRequest(
                    Tail: null,
                    After: secondCursor,
                    Since: since,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Source: null,
                    StackTrace: null,
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityLogsReadResponse payload, out _), Is.True);
            Assert.That(payload.Events.Length, Is.EqualTo(1));
            Assert.That(payload.Events[0].Message, Is.EqualTo("after"));
        });

        private static object CreateValidTestRunPayload (
            bool failFast = false,
            int? timeoutMilliseconds = null)
        {
            return new IpcTestRunRequest(
                TestPlatform: TestRunPlatformCodec.EditMode,
                TestFilter: null,
                TestCategories: Array.Empty<string>(),
                AssemblyNames: Array.Empty<string>(),
                TestSettingsPath: null,
                ResultsXmlPath: "/tmp/results.xml",
                EditorLogPath: "/tmp/editor.log",
                FailFast: failFast,
                RunId: "run-id",
                TimeoutMilliseconds: timeoutMilliseconds);
        }

        private static IpcRequest CreatePingRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.Ping, payload);
        }

        private static UnityEditorReadinessGate CreateReadinessGate (
            UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            Func<bool> isCompilingProvider,
            Func<bool> isUpdatingProvider,
            Func<bool> isPlaymodeActiveProvider)
        {
            return new UnityEditorReadinessGate(
                DaemonEditorMode.Batchmode,
                new UnityEditorLifecycleMonitor(
                    lifecycleTelemetryState,
                    isCompilingProvider,
                    isUpdatingProvider,
                    isPlaymodeActiveProvider,
                    isPlaymodeActiveProvider),
                isPlaymodeActiveProvider,
                new IdleMutationExecutionState(),
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                subscribeToEditorEvents: false);
        }

        private static IpcRequest CreateExecuteRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.Execute, payload);
        }

        private static IpcRequest CreatePlayStatusRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.PlayStatus, payload);
        }

        private static IpcRequest CreateOpsReadRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.OpsRead, payload);
        }

        private static IpcRequest CreateTestRunRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.TestRun, payload);
        }

        private static IpcRequest CreateIndexAssetsReadRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.IndexAssetsRead, payload);
        }

        private static IpcRequest CreateIndexSceneTreeLiteReadRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.IndexSceneTreeLiteRead, payload);
        }

        private static IpcIndexSceneTreeLiteReadResponse CreateIndexSceneTreeLiteReadResponse (
            string scenePath,
            string rootName)
        {
            return new IpcIndexSceneTreeLiteReadResponse(
                GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                ScenePath: scenePath,
                Roots: new[]
                {
                    new IndexSceneTreeLiteNodeJsonContract(rootName, "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>(), IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                },
                SourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false));
        }

        private static IpcRequest CreateShutdownRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.Shutdown, payload);
        }

        private static IpcRequest CreateUnityConsoleClearRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.UnityConsoleClear, payload);
        }

        private static IpcRequest CreateDaemonLogsReadRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.DaemonLogsRead, payload);
        }

        private static IpcRequest CreateUnityLogsReadRequest (
            Guid requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.UnityLogsRead, payload);
        }

        private static DaemonLogsReadUnityIpcMethodHandler CreateDaemonLogsReadHandler (IDaemonLogStream stream)
        {
            return new DaemonLogsReadUnityIpcMethodHandler(
                stream,
                new DaemonLogsReadRequestValidator(),
                new DaemonLogsReadQueryEngine(),
                new DaemonLogsReadResponseFactory(),
                NoOpDaemonLogger.Instance);
        }

        private static OpsReadUnityIpcMethodHandler CreateOpsReadHandler (IUnityEditorReadinessGate readinessGate)
        {
            return new OpsReadUnityIpcMethodHandler(
                new UcliOperationCatalogSnapshot(
                    Array.Empty<UcliOperationRegistration>(),
                    new IpcOpsReadResponse(
                        GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                        Operations: new[]
                        {
                            new IndexOpEntryJsonContract(
                                Name: UcliPrimitiveOperationNames.GoDescribe,
                                Kind: "query",
                                Policy: "safe",
                                ArgsSchemaJson: "{\"type\":\"object\"}"),
                        }),
                    new IpcOpsReadResponse(
                        GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                        Operations: new[]
                        {
                            new IndexOpEntryJsonContract(
                                Name: UcliPrimitiveOperationNames.GoDescribe,
                                Kind: "query",
                                Policy: "safe",
                                ArgsSchemaJson: "{\"type\":\"object\"}"),
                            new IndexOpEntryJsonContract(
                                Name: UcliPrimitiveOperationNames.AssetSave,
                                Kind: "mutation",
                                Policy: "advanced",
                                ArgsSchemaJson: "{\"type\":\"object\"}",
                                Exposure: "editLoweringOnly"),
                        })),
                readinessGate);
        }

        private static TestRunUnityIpcMethodHandler CreateTestRunHandler (
            IUnityTestRunService testRunService,
            IIpcRequestTimeoutScopeFactory timeoutScopeFactory)
        {
            return new TestRunUnityIpcMethodHandler(
                testRunService,
                timeoutScopeFactory);
        }

        private static ExecuteUnityIpcMethodHandler CreateExecuteHandler (
            IExecuteRequestDispatcher executeRequestDispatcher,
            IIpcRequestTimeoutScopeFactory timeoutScopeFactory)
        {
            return new ExecuteUnityIpcMethodHandler(
                executeRequestDispatcher,
                timeoutScopeFactory,
                IpcProjectIdentity.Unknown);
        }

        private static UnityLogsReadUnityIpcMethodHandler CreateUnityLogsReadHandler (IUnityLogStream stream)
        {
            return new UnityLogsReadUnityIpcMethodHandler(
                stream,
                new UnityLogsReadRequestValidator(),
                new UnityLogsReadQueryEngine(),
                new UnityLogsReadResponseFactory(),
                NoOpDaemonLogger.Instance);
        }

        private static UnityConsoleClearUnityIpcMethodHandler CreateUnityConsoleClearHandler (
            IUnityConsoleClearer clearer,
            DaemonEditorMode editorMode)
        {
            return new UnityConsoleClearUnityIpcMethodHandler(
                clearer,
                new StubUnityEditorReadinessGate(editorMode),
                NoOpDaemonLogger.Instance);
        }

        private static IpcRequest CreateRequest (
            Guid requestId,
            string method,
            object payload)
        {
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: "session-token",
                method: method,
                payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: "single");
        }

        private sealed class StubServerVersionProvider : IServerVersionProvider
        {
            private readonly string version;

            public StubServerVersionProvider (string version)
            {
                this.version = version;
            }

            public string GetVersion ()
            {
                return version;
            }
        }

        private sealed class StubAssetLookupSnapshotBuilder : MackySoft.Ucli.Unity.Index.IAssetLookupSnapshotBuilder
        {
            private readonly Func<IpcIndexAssetsReadResponse> build;

            public StubAssetLookupSnapshotBuilder (Func<IpcIndexAssetsReadResponse> build)
            {
                this.build = build;
            }

            public int CallCount { get; private set; }

            public ValueTask<IpcIndexAssetsReadResponse> BuildAsync (CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return new ValueTask<IpcIndexAssetsReadResponse>(build());
            }
        }

        private sealed class StubSceneTreeLiteSnapshotBuilder : MackySoft.Ucli.Unity.Index.ISceneTreeLiteSnapshotBuilder
        {
            private readonly Func<string, IpcIndexSceneTreeLiteReadResponse> build;

            public StubSceneTreeLiteSnapshotBuilder (Func<string, IpcIndexSceneTreeLiteReadResponse> build)
            {
                this.build = build;
            }

            public int CallCount { get; private set; }

            public bool LastLoadedSceneOnly { get; private set; }

            public ValueTask<IpcIndexSceneTreeLiteReadResponse> BuildAsync (
                string scenePath,
                bool loadedSceneOnly = false,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastLoadedSceneOnly = loadedSceneOnly;
                return new ValueTask<IpcIndexSceneTreeLiteReadResponse>(build(scenePath));
            }
        }

        private sealed class StubExecuteRequestDispatcher : IExecuteRequestDispatcher
        {
            private readonly Func<IpcExecuteRequest, ExecuteDispatchContext, CancellationToken, Task<IpcResponse>> execute;

            public StubExecuteRequestDispatcher (
                Func<IpcExecuteRequest, ExecuteDispatchContext, CancellationToken, Task<IpcResponse>> execute)
            {
                this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            }

            public static StubExecuteRequestDispatcher CreateSuccessful ()
            {
                return new StubExecuteRequestDispatcher(DefaultExecuteAsync);
            }

            public int CallCount { get; private set; }

            public IpcExecuteRequest LastRequest { get; private set; }

            public ExecuteDispatchContext LastContext { get; private set; }

            public Task<IpcResponse> DispatchAsync (
                IpcExecuteRequest request,
                ExecuteDispatchContext context,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastRequest = request;
                LastContext = context;
                return execute(request, context, cancellationToken);
            }

            private static Task<IpcResponse> DefaultExecuteAsync (
                IpcExecuteRequest request,
                ExecuteDispatchContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new IpcResponse(
                    protocolVersion: context.ProtocolVersion,
                    requestId: context.RequestId,
                    status: IpcProtocol.StatusOk,
                    payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>())),
                    errors: Array.Empty<IpcError>()));
            }
        }

        private sealed class RecordingUnityGuiBootstrapStarter : IUnityGuiBootstrapStarter
        {
            public List<IpcGuiBootstrapArguments> BootstrapArguments { get; } = new List<IpcGuiBootstrapArguments>();

            public List<UnityGuiSessionReplacementScope> SessionReplacementScopes { get; } = new List<UnityGuiSessionReplacementScope>();

            public List<CancellationToken> CancellationTokens { get; } = new List<CancellationToken>();

            public Task<UnityGuiBootstrapStartResult> StartAsync (
                IpcGuiBootstrapArguments bootstrapArguments,
                UnityGuiSessionReplacementScope sessionReplacementScope,
                CancellationToken cancellationToken)
            {
                BootstrapArguments.Add(bootstrapArguments);
                SessionReplacementScopes.Add(sessionReplacementScope);
                CancellationTokens.Add(cancellationToken);
                return Task.FromResult(UnityGuiBootstrapStartResult.Started());
            }
        }

        private sealed class CancellationObservingUnityGuiBootstrapStarter : IUnityGuiBootstrapStarter
        {
            private readonly TaskCompletionSource<object> startedSource =
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Started => startedSource.Task;

            public CancellationToken CancellationToken { get; private set; }

            public async Task<UnityGuiBootstrapStartResult> StartAsync (
                IpcGuiBootstrapArguments bootstrapArguments,
                UnityGuiSessionReplacementScope sessionReplacementScope,
                CancellationToken cancellationToken)
            {
                CancellationToken = cancellationToken;
                startedSource.TrySetResult(null);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Canceled GUI rebootstrap unexpectedly completed.");
            }
        }

        private sealed class StubUnityTestRunService : IUnityTestRunService
        {
            private readonly Func<IpcTestRunRequest, IUnityTestRunProgressSink, CancellationToken, Task<UnityTestRunServiceResult>> execute;

            public StubUnityTestRunService (Func<IpcTestRunRequest, Task<UnityTestRunServiceResult>> execute)
                : this((request, _, _) => execute(request))
            {
            }

            public StubUnityTestRunService (Func<IpcTestRunRequest, IUnityTestRunProgressSink, Task<UnityTestRunServiceResult>> execute)
                : this((request, progressSink, _) => execute(request, progressSink))
            {
            }

            public StubUnityTestRunService (Func<IpcTestRunRequest, IUnityTestRunProgressSink, CancellationToken, Task<UnityTestRunServiceResult>> execute)
            {
                this.execute = execute;
            }

            public int CallCount { get; private set; }

            public IpcTestRunRequest LastRequest { get; private set; }

            public IUnityTestRunProgressSink LastProgressSink { get; private set; }

            public Task<UnityTestRunServiceResult> ExecuteAsync (
                IpcTestRunRequest request,
                IUnityTestRunProgressSink progressSink = null,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastRequest = request;
                LastProgressSink = progressSink;
                return execute(request, progressSink, cancellationToken);
            }
        }

        private sealed class ManualIpcRequestTimeoutScopeFactory : IIpcRequestTimeoutScopeFactory
        {
            public ManualIpcRequestTimeoutScope LastScope { get; private set; }

            public IIpcRequestTimeoutScope CreateLinked (
                int? timeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                LastScope = new ManualIpcRequestTimeoutScope(cancellationToken);
                return LastScope;
            }
        }

        private sealed class ManualIpcRequestTimeoutScope : IIpcRequestTimeoutScope
        {
            private readonly CancellationTokenSource cancellationTokenSource;
            private readonly CancellationTokenRegistration callerCancellationRegistration;

            private bool disposed;
            private bool isTimeoutCancellationRequested;

            public ManualIpcRequestTimeoutScope (CancellationToken cancellationToken)
            {
                cancellationTokenSource = new CancellationTokenSource();
                callerCancellationRegistration = cancellationToken.Register(
                    static state => ((CancellationTokenSource)state).Cancel(),
                    cancellationTokenSource);
            }

            public CancellationToken Token => cancellationTokenSource.Token;

            public bool IsTimeoutCancellationRequested => isTimeoutCancellationRequested;

            public void CancelForTimeout ()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(ManualIpcRequestTimeoutScope));
                }

                isTimeoutCancellationRequested = true;
                cancellationTokenSource.Cancel();
            }

            public void Dispose ()
            {
                if (disposed)
                {
                    return;
                }

                callerCancellationRegistration.Dispose();
                cancellationTokenSource.Dispose();
                disposed = true;
            }
        }

        private sealed class CollectingIpcStreamFrameWriter : IIpcStreamFrameWriter
        {
            private readonly Guid requestId;
            private readonly Exception progressWriteException;

            public CollectingIpcStreamFrameWriter (
                Guid requestId,
                Exception progressWriteException = null)
            {
                this.requestId = requestId;
                this.progressWriteException = progressWriteException;
            }

            public List<IpcStreamFrame> ProgressFrames { get; } = new List<IpcStreamFrame>();

            public IpcResponse TerminalResponse { get; private set; }

            public int ProgressWriteAttemptCount { get; private set; }

            public ValueTask WriteProgressAsync<TPayload> (
                string eventName,
                TPayload payload,
                CancellationToken cancellationToken = default)
                where TPayload : notnull
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProgressWriteAttemptCount++;
                if (progressWriteException != null)
                {
                    return new ValueTask(Task.FromException(progressWriteException));
                }

                ProgressFrames.Add(new IpcStreamFrame(
                    IpcProtocol.CurrentVersion,
                    requestId,
                    IpcStreamFrameKinds.Progress,
                    eventName,
                    IpcPayloadCodec.SerializeToElement(payload),
                    null));
                return default;
            }

            public ValueTask WriteTerminalAsync (
                IpcResponse response,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TerminalResponse = response;
                return default;
            }
        }

        private sealed class BlockingIpcStreamFrameWriter : IIpcStreamFrameWriter
        {
            private readonly Guid requestId;
            private readonly TaskCompletionSource<bool> writeReleaseSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> firstWriteObservedSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public BlockingIpcStreamFrameWriter (Guid requestId)
            {
                this.requestId = requestId;
            }

            public List<IpcStreamFrame> ProgressFrames { get; } = new List<IpcStreamFrame>();

            public Task FirstWriteObserved => firstWriteObservedSource.Task;

            public CancellationToken LastWriteCancellationToken { get; private set; }

            public async ValueTask WriteProgressAsync<TPayload> (
                string eventName,
                TPayload payload,
                CancellationToken cancellationToken = default)
                where TPayload : notnull
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastWriteCancellationToken = cancellationToken;
                ProgressFrames.Add(new IpcStreamFrame(
                    IpcProtocol.CurrentVersion,
                    requestId,
                    IpcStreamFrameKinds.Progress,
                    eventName,
                    IpcPayloadCodec.SerializeToElement(payload),
                    null));
                firstWriteObservedSource.TrySetResult(true);
                await writeReleaseSource.Task;
            }

            public ValueTask WriteTerminalAsync (
                IpcResponse response,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return default;
            }

            public void ReleaseWrites ()
            {
                writeReleaseSource.TrySetResult(true);
            }
        }

        private sealed class StubUnityConsoleClearer : IUnityConsoleClearer
        {
            private readonly UnityConsoleClearResult result;

            public StubUnityConsoleClearer (UnityConsoleClearResult result)
            {
                this.result = result;
            }

            public int CallCount { get; private set; }

            public UnityConsoleClearResult Clear ()
            {
                CallCount++;
                return result;
            }
        }

        private sealed class IdleMutationExecutionState : IUnityMutationExecutionState
        {
            public bool IsBusy => false;
        }

    }
}
