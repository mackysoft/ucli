using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityOneshotConnectionHandlerTests
    {
        private const string StorageRoot = "oneshot-connection-handler-tests";

        private static readonly DateTimeOffset ObservedUtc =
            new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        private static readonly TimeSpan WatchdogPollInterval = TimeSpan.FromMilliseconds(10);

        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenOneshotStartupPingHandled_KeepsRequestDeadlineAndDoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            var requestExitTimeout = TimeSpan.FromMinutes(1);
            var elapsedTicks = 0L;
            var exitObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc + requestExitTimeout),
                pollInterval: WatchdogPollInterval,
                parentProcessIsSameProcess: static _ => true,
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(
                    () => new TimeSpan(Interlocked.Read(ref elapsedTicks))),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => exitObserved.TrySetResult(true));
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(
                UnityIpcMethod.Ping,
                JsonSerializer.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.OneshotStartup)));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal, watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.Ping));
            Assert.That(completionSignal.IsCompleted, Is.False);
            Interlocked.Exchange(ref elapsedTicks, requestExitTimeout.Ticks);
            Assert.That(exitObserved.Task.Wait(SignalWaitTimeout), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenReadyPingRequestHandled_ReleasesRequestDeadlineWithoutSignalingCompletion () => UniTask.ToCoroutine(async () =>
        {
            var requestExitTimeout = TimeSpan.FromMinutes(1);
            var elapsedTicks = 0L;
            var parentProbeAfterDeadline = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var exitCount = 0;
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc + requestExitTimeout),
                pollInterval: WatchdogPollInterval,
                parentProcessIsSameProcess: _ =>
                {
                    if (Interlocked.Read(ref elapsedTicks) >= requestExitTimeout.Ticks)
                    {
                        parentProbeAfterDeadline.TrySetResult(true);
                    }

                    return true;
                },
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(
                    () => new TimeSpan(Interlocked.Read(ref elapsedTicks))),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => Interlocked.Increment(ref exitCount));
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.Ping, JsonSerializer.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.Ready)));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal, watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.Ping));
            Assert.That(completionSignal.IsCompleted, Is.False);
            Interlocked.Exchange(ref elapsedTicks, requestExitTimeout.Ticks);
            Assert.That(parentProbeAfterDeadline.Task.Wait(SignalWaitTimeout), Is.True);
            Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSuccessfulNonPingRequestHandled_DisablesRequestDeadlineBeforeSignalingCompletion () => UniTask.ToCoroutine(async () =>
        {
            var requestExitTimeout = TimeSpan.FromMinutes(1);
            var elapsedTicks = 0L;
            var parentProbeAfterDeadline = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var exitCount = 0;
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc + requestExitTimeout),
                pollInterval: WatchdogPollInterval,
                parentProcessIsSameProcess: _ =>
                {
                    if (Interlocked.Read(ref elapsedTicks) >= requestExitTimeout.Ticks)
                    {
                        parentProbeAfterDeadline.TrySetResult(true);
                    }

                    return true;
                },
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(
                    () => new TimeSpan(Interlocked.Read(ref elapsedTicks))),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => Interlocked.Increment(ref exitCount));
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal, watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.True);
            Interlocked.Exchange(ref elapsedTicks, requestExitTimeout.Ticks);
            Assert.That(parentProbeAfterDeadline.Task.Wait(SignalWaitTimeout), Is.True);
            Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenShutdownRequestHandled_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.Shutdown, JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")));
            var handler = CreateHandler(request, CreateSuccessResponse(request.RequestId), completionSignal, watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.Shutdown));
            Assert.That(handledResult.IsShutdownAdmissionCommitted, Is.True);
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenShutdownRequestIsRejectedAsEditorBusy_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(
                UnityIpcMethod.Shutdown,
                JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")));
            var handler = CreateHandler(
                request,
                CreateErrorResponse(request.RequestId, EditorLifecycleErrorCodes.EditorBusy),
                completionSignal,
                watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.Shutdown));
            Assert.That(handledResult.IsShutdownAdmissionCommitted, Is.False);
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenNonPingRequestReturnsError_SignalsCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var errorResponse = CreateErrorResponse(request.RequestId, UcliCoreErrorCodes.InvalidArgument);
            var handler = CreateHandler(request, errorResponse, completionSignal, watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSessionTokenFailureHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(
                request,
                CreateErrorResponse(request.RequestId, IpcSessionErrorCodes.SessionTokenInvalid),
                completionSignal,
                watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenProtocolMismatchHandled_DoesNotSignalCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var watchdog = CreateIdleWatchdog();
            var completionSignal = new OneshotRequestCompletionSignal(watchdog);
            var request = CreateRequest(UnityIpcMethod.OpsRead, JsonSerializer.SerializeToElement(new IpcOpsReadRequest()));
            var handler = CreateHandler(
                request,
                CreateErrorResponse(request.RequestId, IpcProtocolErrorCodes.ProtocolVersionMismatch),
                completionSignal,
                watchdog);

            using var stream = await CreateStreamAsync(request);
            var handledResult = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(handledResult.Request, Is.Not.Null);
            Assert.That(handledResult.Method, Is.EqualTo(UnityIpcMethod.OpsRead));
            Assert.That(completionSignal.IsCompleted, Is.False);
        });

        private static OneshotProcessLifetimeWatchdog CreateIdleWatchdog ()
        {
            return new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc.AddMinutes(1)),
                pollInterval: WatchdogPollInterval,
                parentProcessIsSameProcess: static _ => true,
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: static () => { });
        }

        private static IpcOneshotBootstrapEnvelope CreateBootstrapEnvelope (DateTimeOffset exitDeadlineUtc)
        {
            return new IpcOneshotBootstrapEnvelope(
                BootstrapId: Guid.Parse("a23a9990-eed2-4e94-b892-9c7d5609eab4"),
                ParentProcess: new ProcessIdentity(42, 123),
                ProjectFingerprint: new ProjectFingerprint(
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
                SessionToken: IpcSessionToken.CreateRandom(),
                CreatedAtUtc: ObservedUtc.AddMinutes(-10),
                ExitDeadlineUtc: exitDeadlineUtc,
                Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-oneshot-connection-handler-tests"));
        }

        private static UnityOneshotConnectionHandler CreateHandler (
            IpcRequestEnvelope expectedRequest,
            IpcResponse response,
            OneshotRequestCompletionSignal completionSignal,
            OneshotProcessLifetimeWatchdog lifetimeWatchdog)
        {
            return new UnityOneshotConnectionHandler(
                new UnityIpcConnectionHandler(
                    requestHandler: new StubRequestHandler(expectedRequest, response),
                    shutdownAdmissionCoordinator: new PreparedShutdownAdmissionCoordinator(expectedRequest),
                    phaseScopeFactory: new IpcRequestPhaseScopeFactory(),
                    recoverableReplayAvailable: false,
                    initialFrameReadTimeout: UnityIpcConnectionHandler.DefaultInitialFrameReadTimeout,
                    responseFrameWriteTimeout: UnityIpcConnectionHandler.DefaultResponseFrameWriteTimeout),
                completionSignal,
                lifetimeWatchdog);
        }

        private sealed class PreparedShutdownAdmissionCoordinator : IUnityShutdownAdmissionCoordinator
        {
            private readonly Guid preparedRequestId;

            public PreparedShutdownAdmissionCoordinator (IpcRequestEnvelope preparedRequest)
            {
                preparedRequestId = (preparedRequest ?? throw new ArgumentNullException(nameof(preparedRequest))).RequestId;
            }

            public bool TryPrepare (ValidatedUnityIpcRequest request, out string errorMessage)
            {
                errorMessage = null;
                return request != null && request.RequestId == preparedRequestId;
            }

            public bool TryCommit (ValidatedUnityIpcRequest request)
            {
                return request != null && request.RequestId == preparedRequestId;
            }

            public void Abort (ValidatedUnityIpcRequest request)
            {
            }
        }

        private static async Task<MemoryStream> CreateStreamAsync (IpcRequestEnvelope request)
        {
            var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            stream.Position = 0;
            return stream;
        }

        private static IpcRequestEnvelope CreateRequest (
            UnityIpcMethod method,
            JsonElement payload)
        {
            return new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "oneshot",
                method: ContractLiteralCodec.ToValue(method),
                payload: payload,
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
        }

        private sealed class StubRequestHandler : IUnityIpcRequestHandler
        {
            private readonly IpcRequestEnvelope expectedRequest;

            private readonly IpcResponse response;

            public StubRequestHandler (
                IpcRequestEnvelope expectedRequest,
                IpcResponse response)
            {
                this.expectedRequest = expectedRequest;
                this.response = response;
            }

            public Task<UnityIpcRequestValidationResult> ValidateAsync (
                IpcRequestEnvelope request,
                IpcRequestPhaseScope phaseScope)
            {
                phaseScope.ExecutionCancellation.Token.ThrowIfCancellationRequested();
                Assert.That(request.Method, Is.EqualTo(expectedRequest.Method));
                Assert.That(request.RequestId, Is.EqualTo(expectedRequest.RequestId));
                return Task.FromResult(ValidatedUnityIpcRequestTestFactory.Success(request));
            }

            public Task<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestPhaseScope phaseScope)
            {
                phaseScope.ExecutionCancellation.Token.ThrowIfCancellationRequested();
                Assert.That(request.Method, Is.Not.EqualTo(0));
                Assert.That(request.ResponseMode, Is.EqualTo(IpcResponseMode.Single));
                return Task.FromResult(response);
            }

            public Task<IpcResponse> HandleStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestPhaseScope phaseScope)
            {
                throw new NotSupportedException();
            }
        }

        private static IpcResponse CreateSuccessResponse (Guid requestId)
        {
            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                status: IpcResponseStatus.Ok,
                payload: JsonSerializer.SerializeToElement(new { ok = true }),
                errors: System.Array.Empty<IpcError>());
        }

        private static IpcResponse CreateErrorResponse (
            Guid requestId,
            UcliCode errorCode)
        {
            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                status: IpcResponseStatus.Error,
                payload: JsonSerializer.SerializeToElement(new { }),
                errors: new[]
                {
                    new IpcError(errorCode, "error", null),
                });
        }

    }
}
