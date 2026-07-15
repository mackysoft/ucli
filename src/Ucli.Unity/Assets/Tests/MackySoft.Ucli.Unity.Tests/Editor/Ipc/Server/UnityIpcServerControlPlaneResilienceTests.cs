using System;
using System.Collections;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed partial class UnityIpcServerTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator NamedPipeServer_WhenNonCooperativeExecuteDisconnects_ControlPlaneRemainsResponsiveAfterQuarantine () => UniTask.ToCoroutine(async () =>
        {
            var address = "ucli-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using var mutationExecutor = new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations);
            using var controlExecutor = new UnityControlPlaneRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnityControlPlaneRequestExecutor.DefaultMaxConcurrentInvocations);
            using var shutdownAdmissionCoordinator = new UnityShutdownAdmissionCoordinator(mutationExecutor);
            var executeRequestDispatcher = new NonCooperativeMutationExecuteRequestDispatcher(mutationExecutor);
            var shutdownSignal = new StubDaemonShutdownSignal();
            var readinessGate = CreateQuarantineResilienceReadinessGate(mutationExecutor);
            var lifecycleSidecarPersistence = new ResilienceLifecycleSidecarPersistence();
            var lifecycleSidecarWriter = new UnityLifecycleSidecarWriter(
                lifecycleSidecarPersistence,
                new ManualMonotonicClock());
            await lifecycleSidecarWriter.InitializeAsync(
                readinessGate.CaptureAvailabilityObservation(),
                CancellationToken.None);
            using var mutationLifecycleSidecarObserver = new UnityMutationLifecycleSidecarObserver(
                mutationExecutor,
                readinessGate,
                lifecycleSidecarWriter,
                NoOpDaemonLogger.Instance);
            var server = CreateQuarantineResilienceServer(
                executeRequestDispatcher,
                mutationExecutor,
                controlExecutor,
                shutdownAdmissionCoordinator,
                shutdownSignal,
                readinessGate);
            IUnityIpcServerPublicationFence publicationFence = null;

            try
            {
                publicationFence = await TestAwaiter.WaitAsync(
                    server.StartAsync(new IpcEndpoint(IpcTransportKind.NamedPipe, address)).AsUniTask(),
                    "Issue 452 resilience server start",
                    SignalWaitTimeout);

                using var executeClientStream = await OpenNamedPipeRequestAsync(
                    address,
                    CreateNonCooperativeExecuteRequest());
                await TestAwaiter.WaitAsync(
                    executeRequestDispatcher.ExecutionStarted,
                    "Non-cooperative Execute mutation start",
                    SignalWaitTimeout);
                executeClientStream.Dispose();
                await TestAwaiter.WaitAsync(
                    executeRequestDispatcher.CancellationObserved,
                    "Disconnected Execute request cancellation",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(
                    UniTask.WaitUntil(() => mutationExecutor.IsQuarantined),
                    "Mutation-lane quarantine after Execute disconnect",
                    SignalWaitTimeout);

                Assert.That(mutationExecutor.IsQuarantined, Is.True);
                Assert.That(mutationExecutor.HasUnfinishedWork, Is.True);

                var pingResponse = await SendNamedPipeRequestAsync(
                    address,
                    CreatePingRequest(CanonicalSessionToken));
                Assert.That(pingResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(pingResponse.Errors, Is.Empty);
                var pingObservation = pingResponse.Payload.Deserialize<IpcUnityEditorObservation>(SerializerOptions);
                Assert.That(pingObservation, Is.Not.Null);
                Assert.That(
                    pingObservation.State.LifecycleState,
                    Is.EqualTo(IpcEditorLifecycleState.Busy));
                Assert.That(
                    IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(pingObservation.State.LifecycleState),
                    Is.False);

                var playStatusResponse = await SendNamedPipeRequestAsync(
                    address,
                    CreatePlayStatusRequest());
                Assert.That(playStatusResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(playStatusResponse.Errors, Is.Empty);
                var playStatus = playStatusResponse.Payload.Deserialize<IpcPlayStatusResponse>(SerializerOptions);
                Assert.That(playStatus, Is.Not.Null);
                Assert.That(
                    playStatus.Snapshot.State.LifecycleState,
                    Is.EqualTo(pingObservation.State.LifecycleState));
                Assert.That(
                    IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(
                        playStatus.Snapshot.State.LifecycleState),
                    Is.False);

                var persistedObservation = lifecycleSidecarPersistence.LatestSnapshot;
                Assert.That(persistedObservation, Is.Not.Null);
                Assert.That(
                    persistedObservation.State.LifecycleState,
                    Is.EqualTo(pingObservation.State.LifecycleState));
                Assert.That(persistedObservation.CanAcceptExecutionRequests, Is.False);

                var opsReadResponse = await SendNamedPipeRequestAsync(
                    address,
                    CreateOpsReadRequest());
                Assert.That(opsReadResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(opsReadResponse.Errors, Is.Empty);
                var opsCatalog = opsReadResponse.Payload.Deserialize<IpcOpsReadResponse>(SerializerOptions);
                Assert.That(opsCatalog, Is.Not.Null);
                Assert.That(opsCatalog.Operations, Is.Empty);

                var daemonLogsResponse = await SendNamedPipeRequestAsync(
                    address,
                    CreateDaemonLogsReadRequest(CanonicalSessionToken, Guid.NewGuid()));
                Assert.That(daemonLogsResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(daemonLogsResponse.Errors, Is.Empty);
                var daemonLogs = daemonLogsResponse.Payload.Deserialize<IpcDaemonLogsReadResponse>(SerializerOptions);
                Assert.That(daemonLogs, Is.Not.Null);
                Assert.That(daemonLogs.Events.Count, Is.GreaterThanOrEqualTo(1));

                var unityLogsResponse = await SendNamedPipeRequestAsync(
                    address,
                    CreateUnityLogsReadRequest(CanonicalSessionToken, Guid.NewGuid()));
                Assert.That(unityLogsResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(unityLogsResponse.Errors, Is.Empty);
                var unityLogs = unityLogsResponse.Payload.Deserialize<IpcUnityLogsReadResponse>(SerializerOptions);
                Assert.That(unityLogs, Is.Not.Null);
                Assert.That(unityLogs.Events.Count, Is.GreaterThanOrEqualTo(1));

                var shutdownResponse = await SendNamedPipeRequestAsync(
                    address,
                    CreateShutdownRequest(CanonicalSessionToken, Guid.NewGuid()));
                Assert.That(shutdownResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(shutdownResponse.Errors, Is.Empty);
                var shutdown = shutdownResponse.Payload.Deserialize<IpcShutdownResponse>(SerializerOptions);
                Assert.That(shutdown, Is.Not.Null);
                Assert.That(shutdown.Accepted, Is.True);
                await TestAwaiter.WaitAsync(
                    shutdownSignal.SignalObserved,
                    "Shutdown signal after quarantined mutation",
                    SignalWaitTimeout);
            }
            finally
            {
                executeRequestDispatcher.CompleteMutation();
                await TestAwaiter.WaitAsync(
                    mutationExecutor.WaitForRetirementAsync().AsUniTask(),
                    "Non-cooperative Execute mutation retirement",
                    SignalWaitTimeout);
                publicationFence?.Dispose();
                await TestAwaiter.WaitAsync(
                    server.StopAsync().AsUniTask(),
                    "Issue 452 resilience server stop",
                    SignalWaitTimeout);
                await TestAwaiter.WaitAsync(
                    lifecycleSidecarWriter.StopAsync(CancellationToken.None).AsUniTask(),
                    "Issue 452 lifecycle sidecar writer stop",
                    SignalWaitTimeout);
            }

            Assert.That(shutdownSignal.SignalCount, Is.EqualTo(1));
        });

        private static UnityIpcServer CreateQuarantineResilienceServer (
            IExecuteRequestDispatcher executeRequestDispatcher,
            IUnityMainThreadRequestExecutor mutationExecutor,
            IUnityControlPlaneRequestExecutor controlExecutor,
            IUnityShutdownAdmissionCoordinator shutdownAdmissionCoordinator,
            IDaemonShutdownSignal shutdownSignal,
            UnityEditorReadinessGate readinessGate)
        {
            var daemonLogStream = new DaemonLogRingBuffer();
            daemonLogStream.Write("ipc", IpcLogLevel.Info, "server booted");
            var unityLogStream = new UnityLogRingBuffer();
            unityLogStream.Write(
                IpcUnityLogSource.Runtime,
                IpcLogLevel.Info,
                "runtime booted",
                "at Bootstrap.Start()");
            var operationCatalogSnapshot = UcliOperationCatalogSnapshotBuilder.Build(
                Array.Empty<UcliOperationRegistration>());
            var methodDispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new PingUnityIpcMethodHandler(
                        new AssemblyServerVersionProvider(),
                        readinessGate,
                        ProjectIdentity,
                        NoOpDaemonLogger.Instance),
                    new PlayStatusUnityIpcMethodHandler(
                        new AssemblyServerVersionProvider(),
                        readinessGate,
                        ProjectIdentity,
                        NoOpDaemonLogger.Instance),
                    new ExecuteUnityIpcMethodHandler(executeRequestDispatcher, ProjectIdentity),
                    new OpsReadUnityIpcMethodHandler(
                        operationCatalogSnapshot,
                        readinessGate),
                    new DaemonLogsReadUnityIpcMethodHandler(
                        daemonLogStream,
                        new DaemonLogsReadRequestValidator(),
                        new DaemonLogsReadQueryEngine(),
                        new DaemonLogsReadResponseFactory(),
                        NoOpDaemonLogger.Instance),
                    new UnityLogsReadUnityIpcMethodHandler(
                        unityLogStream,
                        new UnityLogsReadRequestValidator(),
                        new UnityLogsReadQueryEngine(),
                        new UnityLogsReadResponseFactory(),
                        NoOpDaemonLogger.Instance),
                    new ShutdownUnityIpcMethodHandler(
                        NoOpDaemonLogger.Instance,
                        shutdownAdmissionCoordinator),
                },
                mutationExecutor,
                controlExecutor,
                recoverableOperationStore: null,
                daemonLogger: NoOpDaemonLogger.Instance);
            var requestHandler = new UnityIpcRequestHandler(
                new StubSessionTokenValidator(accepted: true),
                methodDispatcher,
                NoOpDaemonLogger.Instance);
            var connectionHandler = new UnityIpcConnectionHandler(
                requestHandler,
                shutdownAdmissionCoordinator,
                new IpcRequestPhaseScopeFactory(),
                recoverableReplayAvailable: true,
                UnityIpcConnectionHandler.DefaultInitialFrameReadTimeout,
                UnityIpcConnectionHandler.DefaultResponseFrameWriteTimeout);
            return new UnityIpcServer(
                connectionHandler,
                new IUnityIpcTransportListener[]
                {
                    new NamedPipeUnityIpcTransportListener(
                        NoOpDaemonLogger.Instance,
                        MaximumActiveConnections,
                        ConnectionDrainTimeout),
                },
                shutdownSignal,
                NoOpDaemonLogger.Instance,
                UnityIpcServer.DefaultListenerStopTimeout);
        }

        private static IpcRequestEnvelope CreateNonCooperativeExecuteRequest ()
        {
            var arguments = JsonSerializer.SerializeToElement(
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    ops = Array.Empty<object>(),
                },
                SerializerOptions);
            return CreateResilienceRequest(
                UnityIpcMethod.Execute,
                JsonSerializer.SerializeToElement(
                    new IpcExecuteRequest(UcliCommandIds.Validate.Name, arguments),
                    SerializerOptions),
                TimeSpan.FromSeconds(30));
        }

        private static IpcRequestEnvelope CreateOpsReadRequest ()
        {
            return CreateResilienceRequest(
                UnityIpcMethod.OpsRead,
                JsonSerializer.SerializeToElement(new IpcOpsReadRequest(), SerializerOptions),
                TimeSpan.FromSeconds(30));
        }

        private static IpcRequestEnvelope CreatePlayStatusRequest ()
        {
            return CreateResilienceRequest(
                UnityIpcMethod.PlayStatus,
                JsonSerializer.SerializeToElement(new IpcPlayStatusRequest(), SerializerOptions),
                TimeSpan.FromSeconds(30));
        }

        private static UnityEditorReadinessGate CreateQuarantineResilienceReadinessGate (
            IUnityMutationExecutionState mutationExecutionState)
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 1,
                domainReloadGeneration: 1,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);
            return new UnityEditorReadinessGate(
                DaemonEditorMode.Gui,
                new UnityEditorLifecycleMonitor(
                    telemetryState,
                    static () => false,
                    static () => false,
                    static () => false,
                    static () => false),
                static () => false,
                mutationExecutionState,
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                subscribeToEditorEvents: false);
        }

        private sealed class ResilienceLifecycleSidecarPersistence : IUnityLifecycleSidecarPersistence
        {
            private readonly object syncRoot = new object();

            private UnityEditorObservation latestSnapshot;

            public UnityEditorObservation LatestSnapshot
            {
                get
                {
                    lock (syncRoot)
                    {
                        return latestSnapshot;
                    }
                }
            }

            public Task WriteAsync (
                UnityEditorObservation snapshot,
                DaemonLifecycleRecoveryLease recoveryLease,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (syncRoot)
                {
                    latestSnapshot = snapshot;
                }

                return Task.CompletedTask;
            }

            public Task DeleteIfOwnedAsync (CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private static IpcRequestEnvelope CreateResilienceRequest (
            UnityIpcMethod method,
            JsonElement payload,
            TimeSpan requestDuration)
        {
            var requestDurationMilliseconds = checked((int)requestDuration.TotalMilliseconds);
            return new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: CanonicalSessionToken,
                method: ContractLiteralCodec.ToValue(method),
                payload: payload,
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.UtcNow + requestDuration,
                requestDeadlineRemainingMilliseconds: requestDurationMilliseconds);
        }

        private static async Task<IpcResponse> SendNamedPipeRequestAsync (
            string address,
            IpcRequestEnvelope request)
        {
            using var clientStream = await OpenNamedPipeRequestAsync(address, request);
            using var requestCancellationTokenSource = new CancellationTokenSource(SignalWaitTimeout);
            return await IpcFrameCodec.ReadModelAsync<IpcResponse>(
                clientStream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: requestCancellationTokenSource.Token);
        }

        private static async Task<NamedPipeClientStream> OpenNamedPipeRequestAsync (
            string address,
            IpcRequestEnvelope request)
        {
            var clientStream = new NamedPipeClientStream(
                ".",
                address,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            try
            {
                using var requestCancellationTokenSource = new CancellationTokenSource(SignalWaitTimeout);
                await Task.Run(() => clientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds));
                await IpcFrameCodec.WriteModelAsync(
                    clientStream,
                    request,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: requestCancellationTokenSource.Token);
                return clientStream;
            }
            catch
            {
                clientStream.Dispose();
                throw;
            }
        }

        private sealed class NonCooperativeMutationExecuteRequestDispatcher : IExecuteRequestDispatcher
        {
            private readonly IUnityMutationLaneControl mutationLaneControl;

            private readonly TaskCompletionSource<bool> executionStarted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> mutationCompletion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> cancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public NonCooperativeMutationExecuteRequestDispatcher (IUnityMutationLaneControl mutationLaneControl)
            {
                this.mutationLaneControl = mutationLaneControl;
            }

            public Task ExecutionStarted => executionStarted.Task;

            public Task CancellationObserved => cancellationObserved.Task;

            public async Task<IpcResponse> DispatchAsync (
                IpcExecuteRequest request,
                ExecuteDispatchContext context,
                CancellationToken cancellationToken = default)
            {
                using var cancellationRegistration = cancellationToken.Register(
                    () => cancellationObserved.TrySetResult(true));
                var mutationActivity = mutationLaneControl.BeginMutation();
                executionStarted.TrySetResult(true);
                try
                {
                    // NOTE: Scene reload can continue mutating Unity after request cancellation.
                    // Keep this task non-cooperative until the test explicitly retires the mutation.
                    await mutationCompletion.Task;
                    return new IpcResponse(
                        protocolVersion: IpcProtocol.CurrentVersion,
                        requestId: context.RequestId,
                        status: IpcResponseStatus.Ok,
                        payload: JsonSerializer.SerializeToElement(
                            new IpcExecuteResponse(
                                opResults: Array.Empty<IpcExecuteOperationResult>(),
                                project: context.Project,
                                planToken: null,
                                readPostcondition: null,
                                postReadSource: null,
                                contractViolations: null),
                            SerializerOptions),
                        errors: Array.Empty<IpcError>());
                }
                finally
                {
                    mutationActivity.Complete();
                }
            }

            public void CompleteMutation ()
            {
                mutationCompletion.TrySetResult(true);
            }
        }
    }
}
