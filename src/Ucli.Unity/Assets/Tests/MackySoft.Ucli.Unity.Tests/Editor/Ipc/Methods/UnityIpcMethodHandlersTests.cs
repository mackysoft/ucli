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
            var handler = new PingUnityIpcMethodHandler(new StubServerVersionProvider("1.2.3"), new StubUnityEditorReadinessGate(), "project-fingerprint");
            var request = CreatePingRequest("req-ping-valid", new IpcPingRequest("client"));

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
            Assert.That(payload.PlayMode!.State, Is.EqualTo(IpcPlayModeStateNames.Stopped));
            Assert.That(payload.PlayMode.Transition, Is.EqualTo(IpcPlayModeTransitionNames.None));
            Assert.That(payload.PlayMode.IsPlaying, Is.False);
            Assert.That(payload.PlayMode.IsPlayingOrWillChangePlaymode, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PingHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = new PingUnityIpcMethodHandler(new StubServerVersionProvider("1.2.3"), new StubUnityEditorReadinessGate(), "project-fingerprint");
            var request = CreatePingRequest("req-ping-invalid", 123);

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
                "project-fingerprint");
            var request = CreatePingRequest("req-ping-gui", new IpcPingRequest("client"));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPingResponse payload, out _), Is.True);
            Assert.That(payload.EditorMode, Is.EqualTo(DaemonEditorModeValues.Gui));
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
                "project-fingerprint");

            var firstResponse = await handler.HandleAsync(CreatePingRequest("req-ping-starting-1", new IpcPingRequest("client")), CancellationToken.None);
            var secondResponse = await handler.HandleAsync(CreatePingRequest("req-ping-starting-2", new IpcPingRequest("client")), CancellationToken.None);

            Assert.That(IpcPayloadCodec.TryDeserialize(firstResponse.Payload, out IpcPingResponse firstPayload, out _), Is.True);
            Assert.That(IpcPayloadCodec.TryDeserialize(secondResponse.Payload, out IpcPingResponse secondPayload, out _), Is.True);
            Assert.That(firstPayload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(secondPayload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));

            telemetryState.ObserveEditorUpdate(
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false);
            var readyResponse = await handler.HandleAsync(CreatePingRequest("req-ping-starting-3", new IpcPingRequest("client")), CancellationToken.None);

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
                "project-fingerprint");

            var response = await handler.HandleAsync(CreatePingRequest("req-ping-playmode", new IpcPingRequest("client")), CancellationToken.None);

            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPingResponse payload, out _), Is.True);
            Assert.That(payload.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Playmode));
            Assert.That(payload.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.PlayMode));
            Assert.That(payload.CanAcceptExecutionRequests, Is.False);
            Assert.That(payload.PlayMode, Is.Not.Null);
            Assert.That(payload.PlayMode!.State, Is.EqualTo(IpcPlayModeStateNames.Playing));
            Assert.That(payload.PlayMode.Transition, Is.EqualTo(IpcPlayModeTransitionNames.None));
            Assert.That(payload.PlayMode.IsPlaying, Is.True);
            Assert.That(payload.PlayMode.IsPlayingOrWillChangePlaymode, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PlayStatusHandler_WhenPayloadIsValid_ReturnsLifecycleSnapshotWithoutReadinessWait () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new StubUnityEditorReadinessGate(DaemonEditorMode.Gui);
            var handler = new PlayStatusUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                new IpcProjectIdentity("/repo/UnityProject", "project-fingerprint", "6000.1.4f1"));
            var request = CreatePlayStatusRequest("req-play-status-valid", new IpcPlayStatusRequest());

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(readinessGate.CaptureSnapshotCallCount, Is.EqualTo(1));
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPlayStatusResponse payload, out _), Is.True);
            Assert.That(payload.Snapshot.ServerVersion, Is.EqualTo("1.2.3"));
            Assert.That(payload.Snapshot.EditorMode, Is.EqualTo(DaemonEditorModeValues.Gui));
            Assert.That(payload.Snapshot.UnityVersion, Is.EqualTo("6000.1.4f1"));
            Assert.That(payload.Snapshot.ProjectFingerprint, Is.EqualTo("project-fingerprint"));
            Assert.That(payload.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(payload.Snapshot.CompileState, Is.EqualTo(IpcCompileStateCodec.Ready));
            Assert.That(payload.Snapshot.PlayMode, Is.Not.Null);
            Assert.That(payload.Snapshot.PlayMode!.State, Is.EqualTo(IpcPlayModeStateNames.Stopped));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PlayStatusHandler_WhenPayloadIsInvalid_ReturnsInvalidArgumentWithoutCapturingSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new StubUnityEditorReadinessGate(DaemonEditorMode.Gui);
            var handler = new PlayStatusUnityIpcMethodHandler(
                new StubServerVersionProvider("1.2.3"),
                readinessGate,
                new IpcProjectIdentity("/repo/UnityProject", "project-fingerprint", "6000.1.4f1"));
            var request = CreatePlayStatusRequest("req-play-status-invalid", 123);

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
            var dispatcher = new StubExecuteRequestDispatcher();
            var handler = new ExecuteUnityIpcMethodHandler(dispatcher);
            var request = CreateExecuteRequest(
                "req-execute-valid",
                new IpcExecuteRequest(
                    UcliCommandIds.Validate,
                    IpcPayloadCodec.SerializeToElement(new
                    {
                        protocolVersion = IpcProtocol.CurrentVersion,
                        requestId = "req-execute-valid",
                        ops = Array.Empty<object>(),
                    })));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(dispatcher.CallCount, Is.EqualTo(1));
            Assert.That(dispatcher.LastContext, Is.Not.Null);
            Assert.That(dispatcher.LastContext.RequestId, Is.EqualTo("req-execute-valid"));
            Assert.That(dispatcher.LastRequest, Is.Not.Null);
            Assert.That(dispatcher.LastRequest.Command, Is.EqualTo(UcliCommandIds.Validate.Name));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteHandler_WhenPayloadIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var handler = new ExecuteUnityIpcMethodHandler(new StubExecuteRequestDispatcher());
            var request = CreateExecuteRequest("req-execute-invalid", 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator OpsReadHandler_WhenReady_ReturnsCatalogResponse () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = new StubUnityEditorReadinessGate();
            var handler = CreateOpsReadHandler(readinessGate);
            var request = CreateOpsReadRequest("req-ops-read-ready", new IpcOpsReadRequest());

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
            var request = CreateOpsReadRequest("req-ops-read-validation", new IpcOpsReadRequest(IncludeEditLoweringOnly: true));

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcOpsReadResponse payload, out _), Is.True);
            Assert.That(payload.Operations.Select(static operation => operation.Name), Does.Contain(UcliPrimitiveOperationNames.AssetSave));
            var assetSave = payload.Operations.Single(static operation => operation.Name == UcliPrimitiveOperationNames.AssetSave);
            Assert.That(assetSave.Exposure, Is.EqualTo(UcliOperationExposureValues.EditLoweringOnly));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator OpsReadHandler_WhenFailFastIsDisabled_DelaysResponseUntilReady () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var handler = CreateOpsReadHandler(readinessGate);
            var responseTask = handler.HandleAsync(
                CreateOpsReadRequest("req-ops-read-wait", new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true)),
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
                "req-ops-read-fail-fast",
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
            var handler = CreateTestRunHandler(service);
            var request = CreateTestRunRequest(
                "req-test-run-success",
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
        public IEnumerator TestRunHandler_WhenStreaming_ForwardsProgressFramesAndReturnsOkResponse () => UniTask.ToCoroutine(async () =>
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
            var handler = CreateTestRunHandler(service);
            var streamWriter = new CollectingUnityIpcStreamFrameWriter("req-test-run-stream");
            var request = CreateTestRunRequest(
                "req-test-run-stream",
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
            var handler = CreateTestRunHandler(service);
            var streamWriter = new CollectingUnityIpcStreamFrameWriter(
                "req-test-run-stream-flush-error",
                new IOException("progress write failed"));
            var request = CreateTestRunRequest(
                "req-test-run-stream-flush-error",
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
                "req-test-run-timeout",
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
        public IEnumerator TestRunHandler_WhenStreamingRequestTimeoutElapsesWithPendingProgress_WaitsForFrameAndReturnsIpcTimeout () => UniTask.ToCoroutine(async () =>
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
            var streamWriter = new BlockingUnityIpcStreamFrameWriter("req-test-run-stream-timeout");
            var request = CreateTestRunRequest(
                "req-test-run-stream-timeout",
                CreateValidTestRunPayload(timeoutMilliseconds: 1000));

            var responseTask = handler.HandleStreamingAsync(request, streamWriter, CancellationToken.None).AsTask();
            await TestAwaiter.WaitAsync(streamWriter.FirstWriteObserved, "first blocked progress write", SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                serviceAwaitReadySource.Task,
                "streaming test-run service await point",
                SignalWaitTimeout);
            timeoutScopeFactory.LastScope.CancelForTimeout();
            await TestAwaiter.WaitAsync(serviceCancellationObservedSource.Task, "streaming test-run request timeout", SignalWaitTimeout);

            Assert.That(responseTask.IsCompleted, Is.False);
            Assert.That(streamWriter.LastWriteCancellationToken.IsCancellationRequested, Is.False);

            streamWriter.ReleaseWrites();

            var response = await TestAwaiter.WaitAsync(responseTask, "streaming test-run timeout response", SignalWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
            Assert.That(streamWriter.ProgressFrames.Count, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TestRunProgressSink_WhenPendingFrameLimitIsExceeded_QueuesOneDropDiagnosticAndFlushWaits () => UniTask.ToCoroutine(async () =>
        {
            var streamWriter = new BlockingUnityIpcStreamFrameWriter("req-test-run-progress-backpressure");
            var progressSink = new UnityIpcTestRunProgressSink(
                streamWriter,
                "run-id",
                CancellationToken.None,
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

            var flushTask = progressSink.FlushAsync(CancellationToken.None);

            Assert.That(flushTask.IsCompleted, Is.False);

            streamWriter.ReleaseWrites();
            await flushTask;

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
        public IEnumerator TestRunHandler_WhenServiceReturnsLifecycleFailure_PreservesErrorCode () => UniTask.ToCoroutine(async () =>
        {
            var service = new StubUnityTestRunService(_ => Task.FromResult(UnityTestRunServiceResult.Failure(
                new IpcError(EditorLifecycleErrorCodes.EditorBusy, "Unity editor is busy with internal work.", null))));
            var handler = CreateTestRunHandler(service);
            var request = CreateTestRunRequest(
                "req-test-run-lifecycle-error",
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
            var handler = CreateTestRunHandler(service);
            var request = CreateTestRunRequest(
                "req-test-run-invalid-argument",
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
            var handler = CreateTestRunHandler(service);
            var request = CreateTestRunRequest(
                "req-test-run-internal-error",
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
                new StubUnityTestRunService(request => Task.FromResult(UnityTestRunServiceResult.Success(new IpcTestRunResponse(0)))));
            var request = CreateTestRunRequest("req-test-run-invalid-payload", 123);

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
            var request = CreateIndexAssetsReadRequest("req-index-assets-valid", new IpcIndexAssetsReadRequest());

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
                "req-index-assets-busy",
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
            var request = CreateIndexAssetsReadRequest("req-index-assets-invalid", 123);

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
                "req-index-scene-tree-lite-valid",
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
                "req-index-scene-tree-lite-loaded-only",
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
                "req-index-scene-tree-lite-busy",
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
            var request = CreateIndexSceneTreeLiteReadRequest("req-index-scene-tree-lite-invalid", 123);

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
                "req-index-scene-tree-lite-error",
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
            var handler = new ShutdownUnityIpcMethodHandler();
            var request = CreateShutdownRequest("req-shutdown-valid", new IpcShutdownRequest("tests"));

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
            var handler = new ShutdownUnityIpcMethodHandler();
            var request = CreateShutdownRequest("req-shutdown-invalid", 123);

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator UnityConsoleClearHandler_WhenPayloadIsValidAndEditorModeIsGui_CallsClearerAndReturnsOk () => UniTask.ToCoroutine(async () =>
        {
            var clearer = new StubUnityConsoleClearer(UnityConsoleClearResult.Success());
            var handler = CreateUnityConsoleClearHandler(clearer, DaemonEditorMode.Gui);
            var request = CreateUnityConsoleClearRequest(
                "req-unity-console-clear-valid",
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
            var request = CreateUnityConsoleClearRequest("req-unity-console-clear-invalid", 123);

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
                "req-unity-console-clear-empty-requested-by",
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
                "req-unity-console-clear-invalid-requested-by-character",
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
                "req-unity-console-clear-long-requested-by",
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
                "req-unity-console-clear-batchmode",
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
                "req-unity-console-clear-failure",
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
                "req-daemon-logs-valid",
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
            var request = CreateDaemonLogsReadRequest("req-daemon-logs-invalid", 123);

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
                "req-daemon-logs-stack",
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
                "req-daemon-logs-category-all",
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
                "req-daemon-logs-after-priority",
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
                "req-unity-logs-valid",
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
            var request = CreateUnityLogsReadRequest("req-unity-logs-invalid", 123);

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
                "req-unity-logs-invalid-after",
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
                "req-unity-logs-invalid-source",
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
                "req-unity-logs-invalid-stack-trace",
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
                "req-unity-logs-stack-query",
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
                "req-unity-logs-after-priority",
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
            string requestId,
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
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                subscribeToEditorEvents: false);
        }

        private static IpcRequest CreateExecuteRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.Execute, payload);
        }

        private static IpcRequest CreatePlayStatusRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.PlayStatus, payload);
        }

        private static IpcRequest CreateOpsReadRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.OpsRead, payload);
        }

        private static IpcRequest CreateTestRunRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.TestRun, payload);
        }

        private static IpcRequest CreateIndexAssetsReadRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.IndexAssetsRead, payload);
        }

        private static IpcRequest CreateIndexSceneTreeLiteReadRequest (
            string requestId,
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
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.Shutdown, payload);
        }

        private static IpcRequest CreateUnityConsoleClearRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.UnityConsoleClear, payload);
        }

        private static IpcRequest CreateDaemonLogsReadRequest (
            string requestId,
            object payload)
        {
            return CreateRequest(requestId, IpcMethodNames.DaemonLogsRead, payload);
        }

        private static IpcRequest CreateUnityLogsReadRequest (
            string requestId,
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
                new DaemonLogsReadResponseFactory());
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
                                Exposure: UcliOperationExposureValues.EditLoweringOnly),
                        })),
                readinessGate);
        }

        private static TestRunUnityIpcMethodHandler CreateTestRunHandler (
            IUnityTestRunService testRunService,
            IIpcRequestTimeoutScopeFactory timeoutScopeFactory = null)
        {
            return new TestRunUnityIpcMethodHandler(
                testRunService,
                timeoutScopeFactory ?? new IpcRequestTimeoutScopeFactory());
        }

        private static UnityLogsReadUnityIpcMethodHandler CreateUnityLogsReadHandler (IUnityLogStream stream)
        {
            return new UnityLogsReadUnityIpcMethodHandler(
                stream,
                new UnityLogsReadRequestValidator(),
                new UnityLogsReadQueryEngine(),
                new UnityLogsReadResponseFactory());
        }

        private static UnityConsoleClearUnityIpcMethodHandler CreateUnityConsoleClearHandler (
            IUnityConsoleClearer clearer,
            DaemonEditorMode editorMode)
        {
            return new UnityConsoleClearUnityIpcMethodHandler(
                clearer,
                new StubUnityEditorReadinessGate(editorMode));
        }

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
                Payload: IpcPayloadCodec.SerializeToElement(payload));
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
                return Task.FromResult(new IpcResponse(
                    ProtocolVersion: context.ProtocolVersion,
                    RequestId: context.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>())),
                    Errors: Array.Empty<IpcError>()));
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

        private sealed class CollectingUnityIpcStreamFrameWriter : IUnityIpcStreamFrameWriter
        {
            private readonly string requestId;
            private readonly Exception progressWriteException;

            public CollectingUnityIpcStreamFrameWriter (
                string requestId,
                Exception progressWriteException = null)
            {
                this.requestId = requestId;
                this.progressWriteException = progressWriteException;
            }

            public List<IpcStreamFrame> ProgressFrames { get; } = new List<IpcStreamFrame>();

            public IpcResponse TerminalResponse { get; private set; }

            public int ProgressWriteAttemptCount { get; private set; }

            public Task WriteProgressAsync (
                string eventName,
                object payload,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProgressWriteAttemptCount++;
                if (progressWriteException != null)
                {
                    return Task.FromException(progressWriteException);
                }

                ProgressFrames.Add(new IpcStreamFrame(
                    IpcProtocol.CurrentVersion,
                    requestId,
                    IpcStreamFrameKinds.Progress,
                    eventName,
                    IpcPayloadCodec.SerializeToElement(payload),
                    null));
                return Task.CompletedTask;
            }

            public Task WriteTerminalAsync (
                IpcResponse response,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TerminalResponse = response;
                return Task.CompletedTask;
            }
        }

        private sealed class BlockingUnityIpcStreamFrameWriter : IUnityIpcStreamFrameWriter
        {
            private readonly string requestId;
            private readonly TaskCompletionSource<bool> writeReleaseSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> firstWriteObservedSource =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public BlockingUnityIpcStreamFrameWriter (string requestId)
            {
                this.requestId = requestId;
            }

            public List<IpcStreamFrame> ProgressFrames { get; } = new List<IpcStreamFrame>();

            public Task FirstWriteObserved => firstWriteObservedSource.Task;

            public CancellationToken LastWriteCancellationToken { get; private set; }

            public async Task WriteProgressAsync (
                string eventName,
                object payload,
                CancellationToken cancellationToken = default)
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
                var cancellationSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var cancellationRegistration = cancellationToken.Register(static state =>
                {
                    ((TaskCompletionSource<bool>)state).TrySetResult(true);
                }, cancellationSource);
                var completedTask = await Task.WhenAny(writeReleaseSource.Task, cancellationSource.Task);
                if (!ReferenceEquals(completedTask, writeReleaseSource.Task))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await writeReleaseSource.Task;
                cancellationToken.ThrowIfCancellationRequested();
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

    }
}
