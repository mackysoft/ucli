using System;
using System.Collections;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcConnectionHandlerTests
    {
        private const string CanonicalSessionToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        private const int NamedPipeCancellationStressIterationCount = 16;

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenInitialReadTimeoutExceedsTimerLimit_ThrowsArgumentOutOfRangeException ()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateConnectionHandler(
                requestHandler: new StubRequestHandler(),
                recoverableReplayAvailable: false,
                initialFrameReadTimeout: IpcRequestPhasePlan.MaximumTimerDuration + TimeSpan.FromMilliseconds(1)));

            Assert.That(exception.ParamName, Is.EqualTo("initialFrameReadTimeout"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenMalformedFrameAndErrorResponseWriteFails_DoesNotThrow () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new StubRequestHandler();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false);
            using var stream = new ThrowOnReadAndWriteStream();

            await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(requestHandler.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenMalformedFrameErrorResponseWriteDoesNotComplete_AbortsAtWriteCutoff () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new StubRequestHandler();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false,
                responseFrameWriteTimeout: TimeSpan.FromMilliseconds(25));
            using var stream = new MalformedReadBlockingWriteStream();

            var result = await TestAwaiter.WaitAsync(
                handler.HandleAsync(stream, CancellationToken.None),
                "Bounded malformed-frame response write",
                SignalWaitTimeout);

            Assert.That(result.HasTerminalResponse, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Response, Is.Null);
            await TestAwaiter.WaitAsync(
                stream.Disposed,
                "Malformed-frame response stream cleanup",
                SignalWaitTimeout);
            Assert.That(stream.WasDisposed, Is.True);
            Assert.That(requestHandler.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenConnectedPeerSendsNoInitialFrame_CompletesAfterInitialFrameTimeout () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new StubRequestHandler();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false,
                initialFrameReadTimeout: TimeSpan.FromMilliseconds(25));
            var pipeName = "ucli-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using var serverStream = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            var connectionTask = serverStream.WaitForConnectionAsync(CancellationToken.None);
            using var clientStream = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            clientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds);
            await TestAwaiter.WaitAsync(connectionTask, "Named pipe connection for initial-frame timeout", SignalWaitTimeout);

            var result = await TestAwaiter.WaitAsync(
                handler.HandleAsync(serverStream, CancellationToken.None),
                "Initial IPC frame timeout",
                SignalWaitTimeout);

            Assert.That(result.HasTerminalResponse, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Response, Is.Null);
            Assert.That(requestHandler.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenNamedPipeInitialFrameIsPendingAndLifecycleIsCanceled_ReleasesConnectionAfterTokenSourceDisposal () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new StubRequestHandler();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false,
                initialFrameReadTimeout: TimeSpan.FromSeconds(5));
            var pipeName = "ucli-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using var serverStream = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            var connectionTask = serverStream.WaitForConnectionAsync(CancellationToken.None);
            using var clientStream = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            clientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds);
            await TestAwaiter.WaitAsync(connectionTask, "Named pipe connection for lifecycle cancellation", SignalWaitTimeout);
            var lifecycleCancellationTokenSource = new CancellationTokenSource();
            var handleTask = handler.HandleAsync(serverStream, lifecycleCancellationTokenSource.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(25));

            lifecycleCancellationTokenSource.Cancel();
            lifecycleCancellationTokenSource.Dispose();

            OperationCanceledException cancellationException = null;
            try
            {
                await TestAwaiter.WaitAsync(
                    handleTask,
                    "Canceled initial IPC frame read",
                    SignalWaitTimeout);
            }
            catch (OperationCanceledException exception)
            {
                cancellationException = exception;
            }

            Assert.That(cancellationException, Is.Not.Null);
            Assert.That(requestHandler.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenInitialReadBlocksBeforeReturningTask_StillStopsAtInitialFrameDeadline () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new StubRequestHandler();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false,
                initialFrameReadTimeout: TimeSpan.FromMilliseconds(25));
            using var stream = new SynchronouslyBlockingReadStream();
            var handleTask = Task.Run(() => handler.HandleAsync(stream, CancellationToken.None));
            var completedWithinDeadline = false;

            try
            {
                await TestAwaiter.WaitAsync(
                    stream.ReadStarted,
                    "Synchronous initial read entry",
                    SignalWaitTimeout);
                var completedTask = await Task.WhenAny(
                    handleTask,
                    Task.Delay(TimeSpan.FromSeconds(1)));
                completedWithinDeadline = ReferenceEquals(completedTask, handleTask);
            }
            finally
            {
                stream.AllowReadToReturn();
                await TestAwaiter.WaitAsync(
                    handleTask,
                    "Synchronous initial read test cleanup",
                    SignalWaitTimeout);
            }

            Assert.That(completedWithinDeadline, Is.True);
            Assert.That(requestHandler.CallCount, Is.EqualTo(0));
        });

        [Test]
        [Category("Size.Small")]
        public void Handle_WhenInitialFrameCompletesAfterDeadlineAlreadyWon_DoesNotDispatchLateRequest ()
        {
            var requestHandler = new StubRequestHandler();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false,
                initialFrameReadTimeout: TimeSpan.FromMilliseconds(25));
            var request = CreateShutdownRequest(Guid.NewGuid(), "single", CanonicalSessionToken);
            using var requestBytes = new MemoryStream();
            IpcFrameCodec.WriteModelAsync(
                    requestBytes,
                    request,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            using var stream = new DeadlineRaceReadMemoryStream(requestBytes.ToArray());
            var synchronizationContext = new ManuallyPumpedSynchronizationContext();
            var originalSynchronizationContext = SynchronizationContext.Current;
            Task<UnityIpcConnectionHandleResult> handleTask;
            try
            {
                SynchronizationContext.SetSynchronizationContext(synchronizationContext);
                handleTask = handler.HandleAsync(stream, CancellationToken.None);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(originalSynchronizationContext);
            }

            Assert.That(
                SpinWait.SpinUntil(() => stream.FirstReadStarted.IsCompleted, SignalWaitTimeout),
                Is.True,
                "Initial frame read did not start.");
            Assert.That(
                synchronizationContext.WaitForCallback(SignalWaitTimeout),
                Is.True,
                "Initial-frame deadline continuation was not queued.");
            stream.ReleaseFirstRead();
            PumpNonDeadlineCallbacksUntil(
                synchronizationContext,
                () => stream.RequestBytesConsumed.IsCompleted,
                "Late initial frame was not consumed before the deadline continuation resumed.");
            PumpAllCallbacksExceptOldest(synchronizationContext);

            synchronizationContext.ExecuteOldestCallback();

            Assert.That(handleTask.IsCompleted, Is.True);
            Assert.That(handleTask.GetAwaiter().GetResult().Request, Is.Null);
            Assert.That(requestHandler.CallCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenNamedPipePeerDisconnectsDuringProcessing_CancelsRequest () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new CancellationObservingRequestHandler();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false,
                initialFrameReadTimeout: TimeSpan.FromSeconds(1));
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
            var pipeName = "ucli-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using var serverStream = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            var connectionTask = serverStream.WaitForConnectionAsync(CancellationToken.None);
            using var clientStream = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            clientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds);
            await TestAwaiter.WaitAsync(connectionTask, "Named pipe connection for peer-disconnect test", SignalWaitTimeout);

            var handleTask = handler.HandleAsync(serverStream, CancellationToken.None);
            await IpcFrameCodec.WriteModelAsync(
                clientStream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            await TestAwaiter.WaitAsync(requestHandler.RequestObserved, "IPC request processing start", SignalWaitTimeout);

            clientStream.Dispose();

            await TestAwaiter.WaitAsync(requestHandler.CancellationObserved, "Peer-disconnect cancellation", SignalWaitTimeout);
            var result = await TestAwaiter.WaitAsync(handleTask, "Peer-disconnected request completion", SignalWaitTimeout);

            Assert.That(result.HasTerminalResponse, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Response, Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenCompilePeerDisconnectsWithoutRecoverableReplay_CancelsExecution () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new CancellationObservingRequestHandler();
            var phaseScopeFactory = new RecordingIpcRequestPhaseScopeFactory();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false,
                initialFrameReadTimeout: TimeSpan.FromSeconds(1),
                phaseScopeFactory: phaseScopeFactory);
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Compile),
                payload: JsonSerializer.SerializeToElement(new { }),
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
            var pipeName = "ucli-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using var serverStream = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            var connectionTask = serverStream.WaitForConnectionAsync(CancellationToken.None);
            using var clientStream = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            clientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds);
            await TestAwaiter.WaitAsync(
                connectionTask,
                "Named pipe connection for non-replayable Compile peer-disconnect test",
                SignalWaitTimeout);

            using var hostCancellationTokenSource = new CancellationTokenSource();
            var handleTask = handler.HandleAsync(serverStream, hostCancellationTokenSource.Token);
            await IpcFrameCodec.WriteModelAsync(
                clientStream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            await TestAwaiter.WaitAsync(
                requestHandler.RequestObserved,
                "Non-replayable Compile IPC request processing start",
                SignalWaitTimeout);

            clientStream.Dispose();

            await TestAwaiter.WaitAsync(
                requestHandler.CancellationObserved,
                "Non-replayable Compile peer-disconnect cancellation",
                SignalWaitTimeout);
            var result = await TestAwaiter.WaitAsync(
                handleTask,
                "Peer-disconnected non-replayable Compile request completion",
                SignalWaitTimeout);

            Assert.That(phaseScopeFactory.CreateCalled, Is.True);
            Assert.That(phaseScopeFactory.UpstreamCancellationToken, Is.EqualTo(hostCancellationTokenSource.Token));
            Assert.That(result.HasTerminalResponse, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Response, Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenCompilePeerDisconnectsWithRecoverableReplay_AllowsExecutionToReachTerminalState () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new CompletableRequestHandler();
            var phaseScopeFactory = new RecordingIpcRequestPhaseScopeFactory();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: true,
                initialFrameReadTimeout: TimeSpan.FromSeconds(1),
                phaseScopeFactory: phaseScopeFactory);
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Compile),
                payload: JsonSerializer.SerializeToElement(new { }),
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
            var pipeName = "ucli-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using var serverStream = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            var connectionTask = serverStream.WaitForConnectionAsync(CancellationToken.None);
            using var clientStream = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            clientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds);
            await TestAwaiter.WaitAsync(connectionTask, "Named pipe connection for recoverable peer-disconnect test", SignalWaitTimeout);

            using var hostCancellationTokenSource = new CancellationTokenSource();
            var handleTask = handler.HandleAsync(serverStream, hostCancellationTokenSource.Token);
            await IpcFrameCodec.WriteModelAsync(
                clientStream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            await TestAwaiter.WaitAsync(requestHandler.RequestObserved, "Recoverable IPC request processing start", SignalWaitTimeout);
            var executionUsesHostLifetime = phaseScopeFactory.CreateCalled
                && phaseScopeFactory.UpstreamCancellationToken == hostCancellationTokenSource.Token;

            clientStream.Dispose();
            requestHandler.Complete(request);

            var result = await TestAwaiter.WaitAsync(handleTask, "Peer-disconnected recoverable request completion", SignalWaitTimeout);

            Assert.That(executionUsesHostLifetime, Is.True);
            Assert.That(requestHandler.CancellationObserved.IsCompleted, Is.False);
            Assert.That(requestHandler.CompletionObserved.IsCompleted, Is.True);
            Assert.That(result.HasTerminalResponse, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Response, Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenExecutePeerDisconnectsWithRecoverableReplayAvailable_CancelsExecution () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new CancellationObservingRequestHandler();
            var phaseScopeFactory = new RecordingIpcRequestPhaseScopeFactory();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: true,
                initialFrameReadTimeout: TimeSpan.FromSeconds(1),
                phaseScopeFactory: phaseScopeFactory);
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Execute),
                payload: JsonSerializer.SerializeToElement(new { }),
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
            var pipeName = "ucli-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using var serverStream = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            var connectionTask = serverStream.WaitForConnectionAsync(CancellationToken.None);
            using var clientStream = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            clientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds);
            await TestAwaiter.WaitAsync(
                connectionTask,
                "Named pipe connection for non-recoverable Execute peer-disconnect test",
                SignalWaitTimeout);

            using var hostCancellationTokenSource = new CancellationTokenSource();
            var handleTask = handler.HandleAsync(serverStream, hostCancellationTokenSource.Token);
            await IpcFrameCodec.WriteModelAsync(
                clientStream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            await TestAwaiter.WaitAsync(
                requestHandler.RequestObserved,
                "Non-recoverable Execute IPC request processing start",
                SignalWaitTimeout);

            clientStream.Dispose();

            await TestAwaiter.WaitAsync(
                requestHandler.CancellationObserved,
                "Non-recoverable Execute peer-disconnect cancellation",
                SignalWaitTimeout);
            var result = await TestAwaiter.WaitAsync(
                handleTask,
                "Peer-disconnected non-recoverable Execute request completion",
                SignalWaitTimeout);

            Assert.That(phaseScopeFactory.CreateCalled, Is.True);
            Assert.That(phaseScopeFactory.UpstreamCancellationToken, Is.EqualTo(hostCancellationTokenSource.Token));
            Assert.That(result.HasTerminalResponse, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Response, Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenNamedPipeRequestCompletesWithPendingPeerMonitor_RepeatedlyReleasesConnection () => UniTask.ToCoroutine(async () =>
        {
            for (var iteration = 0; iteration < NamedPipeCancellationStressIterationCount; iteration++)
            {
                var requestHandler = new StubRequestHandler();
                var handler = CreateConnectionHandler(
                    requestHandler: requestHandler,
                    recoverableReplayAvailable: false);
                var request = CreateShutdownRequest(Guid.NewGuid(), "single", CanonicalSessionToken);
                var pipeName = "ucli-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                using var serverStream = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                var connectionTask = serverStream.WaitForConnectionAsync(CancellationToken.None);
                using var clientStream = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                clientStream.Connect((int)SignalWaitTimeout.TotalMilliseconds);
                await TestAwaiter.WaitAsync(
                    connectionTask,
                    $"Named pipe connection for pending peer monitor {iteration}",
                    SignalWaitTimeout);

                var handleTask = handler.HandleAsync(serverStream, CancellationToken.None);
                await IpcFrameCodec.WriteModelAsync(
                    clientStream,
                    request,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: CancellationToken.None);
                var response = await TestAwaiter.WaitAsync(
                    IpcFrameCodec.ReadModelAsync<IpcResponse>(
                            clientStream,
                            IpcJsonSerializerOptions.Default,
                            cancellationToken: CancellationToken.None)
                        .AsTask(),
                    $"Named pipe response for pending peer monitor {iteration}",
                    SignalWaitTimeout);
                var result = await TestAwaiter.WaitAsync(
                    handleTask,
                    $"Named pipe handler completion with pending peer monitor {iteration}",
                    SignalWaitTimeout);

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(result.HasTerminalResponse, Is.True);
                Assert.That(result.Response, Is.Not.Null);
                Assert.That(requestHandler.CallCount, Is.EqualTo(1));
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StreamFrameWriter_WhenProgressWriteFails_InvokesWriteFailureHandler () => UniTask.ToCoroutine(async () =>
        {
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: "stream",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
            using var stream = new ThrowOnWriteStream();
            using var writeCutoffCancellationTokenSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Exception observedFailure = null;
            using var streamWriter = new IpcStreamFrameWriter(
                stream,
                request,
                CancellationToken.None,
                CancellationToken.None,
                writeCutoffCancellationTokenSource.Token,
                exception => observedFailure = exception);

            IOException exception = null;
            try
            {
                await streamWriter.WriteProgressAsync(
                    "test.progress",
                    new UcliEmptyArgs(),
                    CancellationToken.None);
            }
            catch (IOException writeException)
            {
                exception = writeException;
            }

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.Message, Does.Contain("write failed"));
            Assert.That(observedFailure, Is.SameAs(exception));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenShutdownResponseWritten_ReturnsRequestAndResponse () => UniTask.ToCoroutine(async () =>
        {
            using var mutationExecutor = new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations);
            using var shutdownAdmission = new UnityShutdownAdmissionCoordinator(mutationExecutor);
            var requestHandler = new ShutdownPreparingRequestHandler(shutdownAdmission);
            var handler = new UnityIpcConnectionHandler(
                requestHandler: requestHandler,
                shutdownAdmissionCoordinator: shutdownAdmission,
                phaseScopeFactory: new IpcRequestPhaseScopeFactory(),
                recoverableReplayAvailable: false,
                initialFrameReadTimeout: UnityIpcConnectionHandler.DefaultInitialFrameReadTimeout,
                responseFrameWriteTimeout: UnityIpcConnectionHandler.DefaultResponseFrameWriteTimeout);
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);

            using var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            stream.Position = 0;

            var result = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(result.HasTerminalResponse, Is.True);
            Assert.That(result.Request, Is.Not.Null);
            Assert.That(result.Request.Method, Is.EqualTo(UnityIpcMethod.Shutdown));
            Assert.That(result.Response, Is.Not.Null);
            Assert.That(result.Response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(result.Response.Errors, Is.Empty);
            Assert.That(result.IsShutdownAdmissionCommitted, Is.True);
            Assert.That(mutationExecutor.IsBusy, Is.True);

            shutdownAdmission.Dispose();
            Assert.That(mutationExecutor.IsBusy, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenShutdownResponseWriteDoesNotComplete_AbortsAdmissionAtRequestWriteCutoff () => UniTask.ToCoroutine(async () =>
        {
            using var mutationExecutor = new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations);
            using var shutdownAdmission = new UnityShutdownAdmissionCoordinator(mutationExecutor);
            var requestHandler = new ShutdownPreparingRequestHandler(shutdownAdmission);
            var request = CreateShutdownRequest(
                Guid.NewGuid(),
                "single",
                CanonicalSessionToken,
                requestDeadlineRemainingMilliseconds: 250);
            using var requestBytes = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                requestBytes,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            using var stream = new BlockingWriteMemoryStream(requestBytes.ToArray());
            var handler = new UnityIpcConnectionHandler(
                requestHandler: requestHandler,
                shutdownAdmissionCoordinator: shutdownAdmission,
                phaseScopeFactory: new IpcRequestPhaseScopeFactory(),
                recoverableReplayAvailable: false,
                initialFrameReadTimeout: UnityIpcConnectionHandler.DefaultInitialFrameReadTimeout,
                responseFrameWriteTimeout: TimeSpan.FromMilliseconds(25));

            var result = await TestAwaiter.WaitAsync(
                handler.HandleAsync(stream, CancellationToken.None),
                "Bounded shutdown response write",
                SignalWaitTimeout);

            Assert.That(result.HasTerminalResponse, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Response, Is.Null);
            await TestAwaiter.WaitAsync(
                stream.Disposed,
                "Shutdown response stream cleanup",
                SignalWaitTimeout);
            Assert.That(stream.WasDisposed, Is.True);
            Assert.That(mutationExecutor.IsBusy, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSingleResponseWriteIsPendingAndLifecycleIsCanceled_ReleasesStreamWithinDeadline () => UniTask.ToCoroutine(async () =>
        {
            var request = CreateShutdownRequest(Guid.NewGuid(), "single", CanonicalSessionToken);
            using var requestBytes = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                requestBytes,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            using var stream = new BlockingWriteMemoryStream(requestBytes.ToArray());
            var handler = CreateConnectionHandler(
                requestHandler: new StubRequestHandler(),
                recoverableReplayAvailable: false);
            var lifecycleCancellationTokenSource = new CancellationTokenSource();
            var handleTask = handler.HandleAsync(stream, lifecycleCancellationTokenSource.Token);
            await TestAwaiter.WaitAsync(
                stream.WriteStarted,
                "Pending single response write",
                SignalWaitTimeout);

            lifecycleCancellationTokenSource.Cancel();
            lifecycleCancellationTokenSource.Dispose();

            OperationCanceledException cancellationException = null;
            try
            {
                await TestAwaiter.WaitAsync(
                    handleTask,
                    "Canceled single response write",
                    SignalWaitTimeout);
            }
            catch (OperationCanceledException exception)
            {
                cancellationException = exception;
            }

            Assert.That(cancellationException, Is.Not.Null);
            await TestAwaiter.WaitAsync(
                stream.Disposed,
                "Canceled single response stream cleanup",
                SignalWaitTimeout);
            Assert.That(stream.WasDisposed, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenLifecycleIsCanceledBeforeSingleResponseWrite_DoesNotWriteResponseBytes () => UniTask.ToCoroutine(async () =>
        {
            var request = CreateShutdownRequest(Guid.NewGuid(), "single", CanonicalSessionToken);
            using var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            var requestLength = stream.Length;
            stream.Position = 0;
            var lifecycleCancellationTokenSource = new CancellationTokenSource();
            var handler = CreateConnectionHandler(
                requestHandler: new CancelBeforeResponseWriteRequestHandler(lifecycleCancellationTokenSource),
                recoverableReplayAvailable: false);

            OperationCanceledException cancellationException = null;
            try
            {
                await handler.HandleAsync(stream, lifecycleCancellationTokenSource.Token);
            }
            catch (OperationCanceledException exception)
            {
                cancellationException = exception;
            }
            finally
            {
                lifecycleCancellationTokenSource.Dispose();
            }

            Assert.That(cancellationException, Is.Not.Null);
            Assert.That(stream.Length, Is.EqualTo(requestLength));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenResponseWriteBlocksBeforeReturningTask_StillStopsAtRequestWriteCutoff () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new StubRequestHandler();
            var request = CreateShutdownRequest(
                Guid.NewGuid(),
                "single",
                CanonicalSessionToken,
                requestDeadlineRemainingMilliseconds: 250);
            using var requestBytes = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                requestBytes,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            using var stream = new SynchronouslyBlockingWriteMemoryStream(requestBytes.ToArray());
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false,
                responseFrameWriteTimeout: TimeSpan.FromMilliseconds(25));
            var handleTask = Task.Run(() => handler.HandleAsync(stream, CancellationToken.None));
            var completedWithinDeadline = false;

            try
            {
                await TestAwaiter.WaitAsync(
                    stream.WriteStarted,
                    "Synchronous response write entry",
                    SignalWaitTimeout);
                var completedTask = await Task.WhenAny(
                    handleTask,
                    Task.Delay(TimeSpan.FromSeconds(1)));
                completedWithinDeadline = ReferenceEquals(completedTask, handleTask);
            }
            finally
            {
                stream.AllowWriteToReturn();
                await TestAwaiter.WaitAsync(
                    handleTask,
                    "Synchronous response write test cleanup",
                    SignalWaitTimeout);
            }

            Assert.That(completedWithinDeadline, Is.True);
            Assert.That(requestHandler.CallCount, Is.EqualTo(1));
        });

        [Test]
        [Category("Size.Small")]
        public void Handle_WhenResponseWriteCompletesAfterRequestWriteCutoff_DoesNotCommitLateResponse ()
        {
            var requestHandler = new StubRequestHandler();
            var request = CreateShutdownRequest(
                Guid.NewGuid(),
                "single",
                CanonicalSessionToken,
                requestDeadlineRemainingMilliseconds: 250);
            using var requestBytes = new MemoryStream();
            IpcFrameCodec.WriteModelAsync(
                    requestBytes,
                    request,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            using var stream = new DeadlineRaceWriteMemoryStream(requestBytes.ToArray());
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false,
                responseFrameWriteTimeout: TimeSpan.FromMilliseconds(25));
            var synchronizationContext = new ManuallyPumpedSynchronizationContext();
            var originalSynchronizationContext = SynchronizationContext.Current;
            Task<UnityIpcConnectionHandleResult> handleTask;
            try
            {
                SynchronizationContext.SetSynchronizationContext(synchronizationContext);
                handleTask = handler.HandleAsync(stream, CancellationToken.None);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(originalSynchronizationContext);
            }

            PumpCallbacksUntil(
                synchronizationContext,
                () => stream.WriteStarted.IsCompleted,
                "Response write did not start.");
            Assert.That(
                synchronizationContext.WaitForCallback(SignalWaitTimeout),
                Is.True,
                "Response-frame deadline continuation was not queued.");
            stream.ReleaseFirstWrite();
            PumpNonDeadlineCallbacksUntil(
                synchronizationContext,
                () => stream.ResponseBytesWritten.IsCompleted,
                "Late response frame did not finish before the deadline continuation resumed.");
            PumpAllCallbacksExceptOldest(synchronizationContext);

            synchronizationContext.ExecuteOldestCallback();

            Assert.That(handleTask.IsCompleted, Is.True);
            Assert.That(handleTask.GetAwaiter().GetResult().Request, Is.Null);
            Assert.That(requestHandler.CallCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenResponseModeIsStream_WritesProgressAndTerminalFrames () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new StubStreamingRequestHandler();
            var handler = CreateConnectionHandler(
                requestHandler: requestHandler,
                recoverableReplayAvailable: false);
            var requestId = Guid.NewGuid();
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: "token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: "stream",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);

            using var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            var responseStartPosition = stream.Position;
            stream.Position = 0;

            var result = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(result.HasTerminalResponse, Is.True);
            Assert.That(result.Request, Is.Not.Null);
            Assert.That(result.Response, Is.Not.Null);
            Assert.That(requestHandler.CallCount, Is.EqualTo(0));
            Assert.That(requestHandler.StreamingCallCount, Is.EqualTo(1));

            stream.Position = responseStartPosition;
            var progressFrameResult = await IpcFrameCodec.TryReadModelAsync<IpcStreamFrame>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            var terminalFrameResult = await IpcFrameCodec.TryReadModelAsync<IpcStreamFrame>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);

            Assert.That(progressFrameResult.IsSuccess, Is.True);
            Assert.That(progressFrameResult.Value.Kind, Is.EqualTo(IpcStreamFrameKind.Progress));
            Assert.That(progressFrameResult.Value.RequestId, Is.EqualTo(requestId));
            Assert.That(progressFrameResult.Value.Event, Is.EqualTo("test.progress"));

            Assert.That(terminalFrameResult.IsSuccess, Is.True);
            Assert.That(terminalFrameResult.Value.Kind, Is.EqualTo(IpcStreamFrameKind.Terminal));
            Assert.That(terminalFrameResult.Value.Response, Is.Not.Null);
            Assert.That(terminalFrameResult.Value.Response.Status, Is.EqualTo(IpcResponseStatus.Ok));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenSessionTokenIsMissingAndLiteralsAreUnsupported_ReturnsCorrelatedSessionTokenRequired () => UniTask.ToCoroutine(async () =>
        {
            var sessionTokenValidator = new StubSessionTokenValidator(true);
            var dispatcher = new StubMethodDispatcher();
            var handler = CreateConnectionHandler(
                requestHandler: new UnityIpcRequestHandler(
                    sessionTokenValidator,
                    dispatcher,
                    NoOpDaemonLogger.Instance),
                recoverableReplayAvailable: false);
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion + 1,
                requestId: Guid.NewGuid(),
                sessionToken: null,
                method: "unsupported.method",
                payload: JsonSerializer.SerializeToElement(new UcliEmptyArgs()),
                responseMode: "unsupported",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);

            var (result, response) = await HandleRawRequestAsync(
                handler,
                JsonSerializer.SerializeToElement(request));

            Assert.That(result.Request, Is.Null);
            Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenRequired));
            Assert.That(sessionTokenValidator.ValidateCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.DispatchCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.StreamingDispatchCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenProtocolVersionAndLiteralsAreUnsupported_ReturnsCorrelatedProtocolMismatch () => UniTask.ToCoroutine(async () =>
        {
            var sessionTokenValidator = new StubSessionTokenValidator(true);
            var dispatcher = new StubMethodDispatcher();
            var handler = CreateConnectionHandler(
                requestHandler: new UnityIpcRequestHandler(
                    sessionTokenValidator,
                    dispatcher,
                    NoOpDaemonLogger.Instance),
                recoverableReplayAvailable: false);
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion + 1,
                requestId: Guid.NewGuid(),
                sessionToken: CanonicalSessionToken,
                method: "unsupported.method",
                payload: JsonSerializer.SerializeToElement(new UcliEmptyArgs()),
                responseMode: "unsupported",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);

            var (result, response) = await HandleRawRequestAsync(
                handler,
                JsonSerializer.SerializeToElement(request));

            Assert.That(result.Request, Is.Null);
            Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcProtocolErrorCodes.ProtocolVersionMismatch));
            Assert.That(sessionTokenValidator.ValidateCallCount, Is.EqualTo(1));
            Assert.That(dispatcher.DispatchCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.StreamingDispatchCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenStreamRequestProtocolVersionIsUnsupported_WritesCurrentVersionTerminalFrame () => UniTask.ToCoroutine(async () =>
        {
            var requestHandler = new UnityIpcRequestHandler(
                new StubSessionTokenValidator(true),
                new StubMethodDispatcher(),
                NoOpDaemonLogger.Instance);
            var handler = CreateConnectionHandler(
                requestHandler,
                recoverableReplayAvailable: false);
            var requestId = Guid.NewGuid();
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion + 1,
                requestId: requestId,
                sessionToken: CanonicalSessionToken,
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
            using var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            var responseStartPosition = stream.Position;
            stream.Position = 0;

            var result = await handler.HandleAsync(stream, CancellationToken.None);

            Assert.That(result.HasTerminalResponse, Is.True);
            Assert.That(result.Request, Is.Null);
            stream.Position = responseStartPosition;
            var terminalFrame = await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            Assert.That(terminalFrame.ProtocolVersion, Is.EqualTo(IpcProtocol.CurrentVersion));
            Assert.That(terminalFrame.RequestId, Is.EqualTo(requestId));
            Assert.That(terminalFrame.Kind, Is.EqualTo(IpcStreamFrameKind.Terminal));
            Assert.That(terminalFrame.Response, Is.Not.Null);
            Assert.That(terminalFrame.Response.ProtocolVersion, Is.EqualTo(IpcProtocol.CurrentVersion));
            Assert.That(terminalFrame.Response.RequestId, Is.EqualTo(requestId));
            Assert.That(
                terminalFrame.Response.Errors[0].Code,
                Is.EqualTo(IpcProtocolErrorCodes.ProtocolVersionMismatch));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenResponseModeIsUnknownNullOrMissing_ReturnsCorrelatedInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var sessionTokenValidator = new StubSessionTokenValidator(true);
            var dispatcher = new StubMethodDispatcher();
            var handler = CreateConnectionHandler(
                requestHandler: new UnityIpcRequestHandler(
                    sessionTokenValidator,
                    dispatcher,
                    NoOpDaemonLogger.Instance),
                recoverableReplayAvailable: false);
            var deadlineUtc = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
            var requestIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var requests = new[]
            {
                JsonSerializer.SerializeToElement(new IpcRequestEnvelope(
                    IpcProtocol.CurrentVersion,
                    requestIds[0],
                    CanonicalSessionToken,
                    ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                    JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                    "unsupported",
                    deadlineUtc,
                    30_000)),
                JsonSerializer.SerializeToElement(new IpcRequestEnvelope(
                    IpcProtocol.CurrentVersion,
                    requestIds[1],
                    CanonicalSessionToken,
                    ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                    JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                    null,
                    deadlineUtc,
                    30_000)),
                JsonSerializer.SerializeToElement(new
                {
                    ProtocolVersion = IpcProtocol.CurrentVersion,
                    RequestId = requestIds[2],
                    SessionToken = CanonicalSessionToken,
                    Method = ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                    Payload = JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                    RequestDeadlineUtc = deadlineUtc,
                    RequestDeadlineRemainingMilliseconds = 30_000,
                }, IpcJsonSerializerOptions.Default),
            };

            for (var index = 0; index < requests.Length; index++)
            {
                var (result, response) = await HandleRawRequestAsync(handler, requests[index]);

                Assert.That(result.Method, Is.Null, $"index={index}");
                Assert.That(response.RequestId, Is.EqualTo(requestIds[index]), $"index={index}");
                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error), $"index={index}");
                Assert.That(response.Errors.Count, Is.EqualTo(1), $"index={index}");
                Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument), $"index={index}");
            }

            Assert.That(sessionTokenValidator.ValidateCallCount, Is.EqualTo(3));
            Assert.That(dispatcher.DispatchCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.StreamingDispatchCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Handle_WhenMethodIsUnknownNullOrMissing_ReturnsCorrelatedMethodNotSupported () => UniTask.ToCoroutine(async () =>
        {
            var sessionTokenValidator = new StubSessionTokenValidator(true);
            var dispatcher = new StubMethodDispatcher();
            var handler = CreateConnectionHandler(
                requestHandler: new UnityIpcRequestHandler(
                    sessionTokenValidator,
                    dispatcher,
                    NoOpDaemonLogger.Instance),
                recoverableReplayAvailable: false);
            var deadlineUtc = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
            var requestIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var requests = new[]
            {
                JsonSerializer.SerializeToElement(new IpcRequestEnvelope(
                    IpcProtocol.CurrentVersion,
                    requestIds[0],
                    CanonicalSessionToken,
                    "unsupported.method",
                    JsonSerializer.SerializeToElement(new UcliEmptyArgs()),
                    ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                    deadlineUtc,
                    30_000)),
                JsonSerializer.SerializeToElement(new IpcRequestEnvelope(
                    IpcProtocol.CurrentVersion,
                    requestIds[1],
                    CanonicalSessionToken,
                    null,
                    JsonSerializer.SerializeToElement(new UcliEmptyArgs()),
                    ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                    deadlineUtc,
                    30_000)),
                JsonSerializer.SerializeToElement(new
                {
                    ProtocolVersion = IpcProtocol.CurrentVersion,
                    RequestId = requestIds[2],
                    SessionToken = CanonicalSessionToken,
                    Payload = JsonSerializer.SerializeToElement(new UcliEmptyArgs()),
                    ResponseMode = ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                    RequestDeadlineUtc = deadlineUtc,
                    RequestDeadlineRemainingMilliseconds = 30_000,
                }, IpcJsonSerializerOptions.Default),
            };

            for (var index = 0; index < requests.Length; index++)
            {
                var (result, response) = await HandleRawRequestAsync(handler, requests[index]);

                Assert.That(result.Method, Is.Null, $"index={index}");
                Assert.That(response.RequestId, Is.EqualTo(requestIds[index]), $"index={index}");
                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error), $"index={index}");
                Assert.That(response.Errors.Count, Is.EqualTo(1), $"index={index}");
                Assert.That(response.Errors[0].Code, Is.EqualTo(IpcProtocolErrorCodes.IpcMethodNotSupported), $"index={index}");
            }

            Assert.That(sessionTokenValidator.ValidateCallCount, Is.EqualTo(3));
            Assert.That(dispatcher.DispatchCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.StreamingDispatchCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestHandler_WhenUnauthenticatedMethodContainsNewlineAndLongLiteral_DoesNotLogUntrustedLiteral () => UniTask.ToCoroutine(async () =>
        {
            var daemonLogger = new RecordingDaemonLogger();
            var handler = new UnityIpcRequestHandler(
                new StubSessionTokenValidator(true),
                new StubMethodDispatcher(),
                daemonLogger);
            var untrustedMarker = "untrusted-pre-auth-method-marker";
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: null,
                method: untrustedMarker + "\n" + new string('m', 4096),
                payload: JsonSerializer.SerializeToElement(new UcliEmptyArgs()),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);

            var response = await HandleRequestAsync(handler, request, CancellationToken.None);

            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenRequired));
            Assert.That(daemonLogger.WarningCallCount, Is.EqualTo(1));
            Assert.That(daemonLogger.LastWarningMessage, Does.Not.Contain(untrustedMarker));
            Assert.That(daemonLogger.LastWarningMessage, Does.Not.Contain("\n"));
            Assert.That(daemonLogger.LastWarningRaw, Does.Not.Contain(untrustedMarker));
            Assert.That(daemonLogger.LastWarningRaw, Does.Not.Contain("\n"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestHandler_WhenMethodContainsNewlineAndLongLiteral_DoesNotReflectUntrustedLiteral () => UniTask.ToCoroutine(async () =>
        {
            var daemonLogger = new RecordingDaemonLogger();
            var handler = new UnityIpcRequestHandler(
                new StubSessionTokenValidator(true),
                new StubMethodDispatcher(),
                daemonLogger);
            var untrustedMarker = "untrusted-method-marker";
            var request = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: CanonicalSessionToken,
                method: untrustedMarker + "\n" + new string('m', 4096),
                payload: JsonSerializer.SerializeToElement(new UcliEmptyArgs()),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);

            var response = await HandleRequestAsync(handler, request, CancellationToken.None);

            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcProtocolErrorCodes.IpcMethodNotSupported));
            Assert.That(response.Errors[0].Message, Does.Not.Contain(untrustedMarker));
            Assert.That(response.Errors[0].Message, Does.Not.Contain("\n"));
            Assert.That(daemonLogger.LastWarningMessage, Does.Not.Contain(untrustedMarker));
            Assert.That(daemonLogger.LastWarningMessage, Does.Not.Contain("\n"));
            Assert.That(daemonLogger.LastWarningRaw, Does.Not.Contain(untrustedMarker));
            Assert.That(daemonLogger.LastWarningRaw, Does.Not.Contain("\n"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestHandler_WhenResponseModeContainsNewlineAndLongLiteral_DoesNotReflectUntrustedLiteral () => UniTask.ToCoroutine(async () =>
        {
            var daemonLogger = new RecordingDaemonLogger();
            var handler = new UnityIpcRequestHandler(
                new StubSessionTokenValidator(true),
                new StubMethodDispatcher(),
                daemonLogger);
            var untrustedMarker = "untrusted-response-mode-marker";
            var request = CreateShutdownRequest(
                Guid.NewGuid(),
                untrustedMarker + "\n" + new string('r', 4096),
                CanonicalSessionToken);

            var response = await HandleRequestAsync(handler, request, CancellationToken.None);

            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].Message, Does.Not.Contain(untrustedMarker));
            Assert.That(response.Errors[0].Message, Does.Not.Contain("\n"));
            Assert.That(daemonLogger.LastWarningMessage, Does.Not.Contain(untrustedMarker));
            Assert.That(daemonLogger.LastWarningMessage, Does.Not.Contain("\n"));
            Assert.That(daemonLogger.LastWarningRaw, Does.Not.Contain(untrustedMarker));
            Assert.That(daemonLogger.LastWarningRaw, Does.Not.Contain("\n"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestHandler_WhenSessionTokenIsCanonical_PassesParsedTokenToValidator () => UniTask.ToCoroutine(async () =>
        {
            var sessionTokenValidator = new StubSessionTokenValidator(true);
            var dispatcher = new StubMethodDispatcher();
            var handler = new UnityIpcRequestHandler(
                sessionTokenValidator,
                dispatcher,
                NoOpDaemonLogger.Instance);
            var request = CreateShutdownRequest(Guid.NewGuid(), "single", CanonicalSessionToken);

            var response = await HandleRequestAsync(handler, request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(sessionTokenValidator.ValidateCallCount, Is.EqualTo(1));
            Assert.That(
                sessionTokenValidator.LastSessionToken.GetEncodedValue(),
                Is.EqualTo(CanonicalSessionToken));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestHandler_WhenSingleRequestSessionTokenIsNonCanonical_ReturnsCorrelatedInvalidTokenWithoutValidation () => UniTask.ToCoroutine(async () =>
        {
            var sessionTokenValidator = new StubSessionTokenValidator(true);
            var dispatcher = new StubMethodDispatcher();
            var handler = new UnityIpcRequestHandler(
                sessionTokenValidator,
                dispatcher,
                NoOpDaemonLogger.Instance);
            var request = CreateShutdownRequest(Guid.NewGuid(), "single", "not-canonical");

            var response = await HandleRequestAsync(handler, request, CancellationToken.None);

            Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenInvalid));
            Assert.That(sessionTokenValidator.ValidateCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.DispatchCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.StreamingDispatchCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestHandler_WhenStreamingRequestSessionTokenIsNonCanonical_ReturnsCorrelatedInvalidTokenWithoutValidation () => UniTask.ToCoroutine(async () =>
        {
            var sessionTokenValidator = new StubSessionTokenValidator(true);
            var dispatcher = new StubMethodDispatcher();
            var handler = new UnityIpcRequestHandler(
                sessionTokenValidator,
                dispatcher,
                NoOpDaemonLogger.Instance);
            var request = CreateShutdownRequest(Guid.NewGuid(), "stream", "not-canonical");

            var response = await HandleStreamingRequestAsync(
                handler,
                request,
                new NoOpStreamFrameWriter(),
                CancellationToken.None);

            Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenInvalid));
            Assert.That(sessionTokenValidator.ValidateCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.DispatchCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.StreamingDispatchCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestHandler_WhenSingleRequestSessionTokenIsMissing_ReturnsCorrelatedRequiredTokenWithoutValidation () => UniTask.ToCoroutine(async () =>
        {
            var sessionTokenValidator = new StubSessionTokenValidator(true);
            var dispatcher = new StubMethodDispatcher();
            var handler = new UnityIpcRequestHandler(
                sessionTokenValidator,
                dispatcher,
                NoOpDaemonLogger.Instance);

            foreach (var missingSessionToken in new[] { null, string.Empty, "   " })
            {
                var request = CreateShutdownRequest(Guid.NewGuid(), "single", missingSessionToken);

                var response = await HandleRequestAsync(handler, request, CancellationToken.None);

                Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenRequired));
            }

            Assert.That(sessionTokenValidator.ValidateCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.DispatchCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.StreamingDispatchCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RequestHandler_WhenStreamingRequestSessionTokenIsMissing_ReturnsCorrelatedRequiredTokenWithoutValidation () => UniTask.ToCoroutine(async () =>
        {
            var sessionTokenValidator = new StubSessionTokenValidator(true);
            var dispatcher = new StubMethodDispatcher();
            var handler = new UnityIpcRequestHandler(
                sessionTokenValidator,
                dispatcher,
                NoOpDaemonLogger.Instance);

            foreach (var missingSessionToken in new[] { null, string.Empty, "   " })
            {
                var request = CreateShutdownRequest(Guid.NewGuid(), "stream", missingSessionToken);

                var response = await HandleStreamingRequestAsync(
                    handler,
                    request,
                    new NoOpStreamFrameWriter(),
                    CancellationToken.None);

                Assert.That(response.RequestId, Is.EqualTo(request.RequestId));
                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(response.Errors.Count, Is.EqualTo(1));
                Assert.That(response.Errors[0].Code, Is.EqualTo(IpcSessionErrorCodes.SessionTokenRequired));
            }

            Assert.That(sessionTokenValidator.ValidateCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.DispatchCallCount, Is.EqualTo(0));
            Assert.That(dispatcher.StreamingDispatchCallCount, Is.EqualTo(0));
        });

        private static IpcRequestEnvelope CreateShutdownRequest (
            Guid requestId,
            string responseMode,
            string sessionToken,
            int requestDeadlineRemainingMilliseconds = 30_000)
        {
            return new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: sessionToken,
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown),
                payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: responseMode,
                requestDeadlineUtc: DateTimeOffset.UtcNow
                    + TimeSpan.FromMilliseconds(requestDeadlineRemainingMilliseconds),
                requestDeadlineRemainingMilliseconds: requestDeadlineRemainingMilliseconds);
        }

        private static async Task<IpcResponse> HandleRequestAsync (
            UnityIpcRequestHandler requestHandler,
            IpcRequestEnvelope request,
            CancellationToken cancellationToken)
        {
            using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                request,
                cancellationToken,
                TimeSpan.FromSeconds(1));
            var validationResult = await requestHandler.ValidateAsync(request, phaseScope);
            if (!validationResult.IsSuccess)
            {
                return validationResult.ErrorResponse;
            }

            return await requestHandler.HandleAsync(
                validationResult.Request,
                phaseScope);
        }

        private static async Task<(UnityIpcConnectionHandleResult Result, IpcResponse Response)> HandleRawRequestAsync (
            UnityIpcConnectionHandler connectionHandler,
            JsonElement request)
        {
            using var stream = new MemoryStream();
            await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            var responseStartPosition = stream.Position;
            stream.Position = 0;

            var result = await connectionHandler.HandleAsync(stream, CancellationToken.None);

            stream.Position = responseStartPosition;
            var response = await IpcFrameCodec.ReadModelAsync<IpcResponse>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: CancellationToken.None);
            return (result, response);
        }

        private static async Task<IpcResponse> HandleStreamingRequestAsync (
            UnityIpcRequestHandler requestHandler,
            IpcRequestEnvelope request,
            IIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken)
        {
            using var phaseScope = new IpcRequestPhaseScopeFactory().Create(
                request,
                cancellationToken,
                TimeSpan.FromSeconds(1));
            var validationResult = await requestHandler.ValidateAsync(request, phaseScope);
            if (!validationResult.IsSuccess)
            {
                return validationResult.ErrorResponse;
            }

            return await requestHandler.HandleStreamingAsync(
                validationResult.Request,
                streamWriter,
                phaseScope);
        }

        private static UnityIpcConnectionHandler CreateConnectionHandler (
            IUnityIpcRequestHandler requestHandler,
            bool recoverableReplayAvailable,
            TimeSpan? initialFrameReadTimeout = null,
            TimeSpan? responseFrameWriteTimeout = null,
            IIpcRequestPhaseScopeFactory phaseScopeFactory = null)
        {
            return new UnityIpcConnectionHandler(
                requestHandler: requestHandler,
                shutdownAdmissionCoordinator: new StrictShutdownAdmissionCoordinator(),
                phaseScopeFactory: phaseScopeFactory ?? new IpcRequestPhaseScopeFactory(),
                recoverableReplayAvailable: recoverableReplayAvailable,
                initialFrameReadTimeout: initialFrameReadTimeout ?? UnityIpcConnectionHandler.DefaultInitialFrameReadTimeout,
                responseFrameWriteTimeout: responseFrameWriteTimeout ?? UnityIpcConnectionHandler.DefaultResponseFrameWriteTimeout);
        }

        private static void PumpNonDeadlineCallbacksUntil (
            ManuallyPumpedSynchronizationContext synchronizationContext,
            Func<bool> completionCondition,
            string failureMessage)
        {
            var deadlineUtc = DateTime.UtcNow + SignalWaitTimeout;
            while (!completionCondition() && DateTime.UtcNow < deadlineUtc)
            {
                if (synchronizationContext.PendingCallbackCount > 1)
                {
                    synchronizationContext.ExecuteNewestCallback();
                }
                else
                {
                    Thread.Yield();
                }
            }

            Assert.That(completionCondition(), Is.True, failureMessage);
        }

        private static void PumpCallbacksUntil (
            ManuallyPumpedSynchronizationContext synchronizationContext,
            Func<bool> completionCondition,
            string failureMessage)
        {
            var deadlineUtc = DateTime.UtcNow + SignalWaitTimeout;
            while (!completionCondition() && DateTime.UtcNow < deadlineUtc)
            {
                if (synchronizationContext.PendingCallbackCount > 0)
                {
                    synchronizationContext.ExecuteOldestCallback();
                }
                else
                {
                    Thread.Yield();
                }
            }

            Assert.That(completionCondition(), Is.True, failureMessage);
        }

        private static void PumpAllCallbacksExceptOldest (
            ManuallyPumpedSynchronizationContext synchronizationContext)
        {
            while (synchronizationContext.PendingCallbackCount > 1)
            {
                synchronizationContext.ExecuteNewestCallback();
            }
        }

        private sealed class StrictShutdownAdmissionCoordinator : IUnityShutdownAdmissionCoordinator
        {
            private ValidatedUnityIpcRequest preparedRequest;

            public bool TryPrepare (ValidatedUnityIpcRequest request, out string errorMessage)
            {
                preparedRequest = request;
                errorMessage = null;
                return true;
            }

            public bool TryCommit (ValidatedUnityIpcRequest request)
            {
                return ReferenceEquals(preparedRequest, request);
            }

            public void Abort (ValidatedUnityIpcRequest request)
            {
                if (ReferenceEquals(preparedRequest, request))
                {
                    preparedRequest = null;
                }
            }
        }

        private sealed class StubRequestHandler : ValidatedUnityIpcRequestHandlerStub
        {
            public int CallCount { get; private set; }

            public UnityIpcMethod? LastMethod { get; private set; }

            public IpcResponseMode? LastResponseMode { get; private set; }

            public override Task<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestPhaseScope phaseScope)
            {
                CallCount++;
                LastMethod = request.Method;
                LastResponseMode = request.ResponseMode;
                return Task.FromResult(new IpcResponse(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: request.RequestId,
                    status: IpcResponseStatus.Ok,
                    payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(true, "ok")),
                    errors: Array.Empty<IpcError>()));
            }

            public override Task<IpcResponse> HandleStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestPhaseScope phaseScope)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class CancelBeforeResponseWriteRequestHandler : ValidatedUnityIpcRequestHandlerStub
        {
            private readonly CancellationTokenSource lifecycleCancellationTokenSource;

            public CancelBeforeResponseWriteRequestHandler (
                CancellationTokenSource lifecycleCancellationTokenSource)
            {
                this.lifecycleCancellationTokenSource = lifecycleCancellationTokenSource;
            }

            public override Task<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestPhaseScope phaseScope)
            {
                lifecycleCancellationTokenSource.Cancel();
                return Task.FromResult(new IpcResponse(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: request.RequestId,
                    status: IpcResponseStatus.Ok,
                    payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(true, "ok")),
                    errors: Array.Empty<IpcError>()));
            }

            public override Task<IpcResponse> HandleStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestPhaseScope phaseScope)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ShutdownPreparingRequestHandler : ValidatedUnityIpcRequestHandlerStub
        {
            private readonly IUnityShutdownAdmissionCoordinator shutdownAdmissionCoordinator;

            public ShutdownPreparingRequestHandler (IUnityShutdownAdmissionCoordinator shutdownAdmissionCoordinator)
            {
                this.shutdownAdmissionCoordinator = shutdownAdmissionCoordinator;
            }

            public override Task<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestPhaseScope phaseScope)
            {
                phaseScope.ExecutionCancellation.Token.ThrowIfCancellationRequested();
                Assert.That(request.Method, Is.EqualTo(UnityIpcMethod.Shutdown));
                Assert.That(request.ResponseMode, Is.EqualTo(IpcResponseMode.Single));
                Assert.That(shutdownAdmissionCoordinator.TryPrepare(request, out var errorMessage), Is.True, errorMessage);
                return Task.FromResult(new IpcResponse(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: request.RequestId,
                    status: IpcResponseStatus.Ok,
                    payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(true, "ok")),
                    errors: Array.Empty<IpcError>()));
            }

            public override Task<IpcResponse> HandleStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestPhaseScope phaseScope)
            {
                throw new NotSupportedException();
            }
        }

        private class BlockingWriteMemoryStream : MemoryStream
        {
            private readonly TaskCompletionSource<bool> writeStarted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> writeRelease =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> disposed =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public BlockingWriteMemoryStream (byte[] requestBytes)
                : base(requestBytes, writable: false)
            {
            }

            public Task WriteStarted => writeStarted.Task;

            public Task Disposed => disposed.Task;

            public bool WasDisposed { get; private set; }

            public override ValueTask WriteAsync (
                ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                writeStarted.TrySetResult(true);
                return new ValueTask(writeRelease.Task);
            }

            protected override void Dispose (bool disposing)
            {
                WasDisposed = true;
                disposed.TrySetResult(true);
                writeRelease.TrySetException(new ObjectDisposedException(nameof(BlockingWriteMemoryStream)));
                base.Dispose(disposing);
            }
        }

        private sealed class SynchronouslyBlockingReadStream : Stream
        {
            private readonly TaskCompletionSource<bool> readStarted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly ManualResetEventSlim readRelease = new ManualResetEventSlim();

            public Task ReadStarted => readStarted.Task;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => 0;

            public override long Position
            {
                get => 0;
                set => throw new NotSupportedException();
            }

            public void AllowReadToReturn ()
            {
                readRelease.Set();
            }

            public override void Flush ()
            {
            }

            public override int Read (
                byte[] buffer,
                int offset,
                int count)
            {
                readStarted.TrySetResult(true);
                readRelease.Wait();
                return 0;
            }

            public override ValueTask<int> ReadAsync (
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                readStarted.TrySetResult(true);
                readRelease.Wait();
                return new ValueTask<int>(0);
            }

            public override long Seek (
                long offset,
                SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength (long value)
            {
                throw new NotSupportedException();
            }

            public override void Write (
                byte[] buffer,
                int offset,
                int count)
            {
            }

            public override ValueTask WriteAsync (
                ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                return default;
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    readRelease.Set();
                }

                base.Dispose(disposing);
            }
        }

        private sealed class SynchronouslyBlockingWriteMemoryStream : MemoryStream
        {
            private readonly TaskCompletionSource<bool> writeStarted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly ManualResetEventSlim writeRelease = new ManualResetEventSlim();

            public SynchronouslyBlockingWriteMemoryStream (byte[] requestBytes)
                : base(requestBytes, writable: false)
            {
            }

            public Task WriteStarted => writeStarted.Task;

            public void AllowWriteToReturn ()
            {
                writeRelease.Set();
            }

            public override ValueTask WriteAsync (
                ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                writeStarted.TrySetResult(true);
                writeRelease.Wait();
                return default;
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    writeRelease.Set();
                }

                base.Dispose(disposing);
            }
        }

        private sealed class DeadlineRaceReadMemoryStream : MemoryStream
        {
            private readonly object syncRoot = new object();

            private readonly TaskCompletionSource<int> firstReadCompletion =
                new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> firstReadStarted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> requestBytesConsumed =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private Memory<byte> pendingFirstReadBuffer;

            private bool firstReadPending;

            public DeadlineRaceReadMemoryStream (byte[] requestBytes)
                : base(requestBytes, writable: false)
            {
            }

            public Task RequestBytesConsumed => requestBytesConsumed.Task;

            public Task FirstReadStarted => firstReadStarted.Task;

            public override ValueTask<int> ReadAsync (
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                lock (syncRoot)
                {
                    if (!firstReadPending && Position == 0)
                    {
                        firstReadPending = true;
                        pendingFirstReadBuffer = buffer;
                        firstReadStarted.TrySetResult(true);
                        return new ValueTask<int>(firstReadCompletion.Task);
                    }
                }

                var readLength = Read(buffer.Span);
                if (Position == Length)
                {
                    requestBytesConsumed.TrySetResult(true);
                }

                return new ValueTask<int>(readLength);
            }

            public void ReleaseFirstRead ()
            {
                int readLength;
                lock (syncRoot)
                {
                    readLength = Read(pendingFirstReadBuffer.Span);
                }

                firstReadCompletion.TrySetResult(readLength);
            }

            public override ValueTask WriteAsync (
                ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                return default;
            }
        }

        private sealed class DeadlineRaceWriteMemoryStream : MemoryStream
        {
            private readonly TaskCompletionSource<bool> writeStarted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> firstWriteRelease =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> responseBytesWritten =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private int writeCallCount;

            public DeadlineRaceWriteMemoryStream (byte[] requestBytes)
                : base(requestBytes, writable: false)
            {
            }

            public Task WriteStarted => writeStarted.Task;

            public Task ResponseBytesWritten => responseBytesWritten.Task;

            public void ReleaseFirstWrite ()
            {
                firstWriteRelease.TrySetResult(true);
            }

            public override ValueTask WriteAsync (
                ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                var callCount = Interlocked.Increment(ref writeCallCount);
                writeStarted.TrySetResult(true);
                if (callCount == 1)
                {
                    return new ValueTask(CompleteFirstWriteAsync());
                }

                responseBytesWritten.TrySetResult(true);
                return default;
            }

            private async Task CompleteFirstWriteAsync ()
            {
                await firstWriteRelease.Task;
            }
        }

        private sealed class ManuallyPumpedSynchronizationContext : SynchronizationContext
        {
            private readonly object syncRoot = new object();

            private readonly System.Collections.Generic.List<(SendOrPostCallback Callback, object State)> callbacks =
                new System.Collections.Generic.List<(SendOrPostCallback Callback, object State)>();

            private readonly ManualResetEventSlim callbackAvailable = new ManualResetEventSlim();

            public int PendingCallbackCount
            {
                get
                {
                    lock (syncRoot)
                    {
                        return callbacks.Count;
                    }
                }
            }

            public override void Post (
                SendOrPostCallback d,
                object state)
            {
                lock (syncRoot)
                {
                    callbacks.Add((d, state));
                    callbackAvailable.Set();
                }
            }

            public bool WaitForCallback (TimeSpan timeout)
            {
                return callbackAvailable.Wait(timeout);
            }

            public void ExecuteOldestCallback ()
            {
                ExecuteCallbackAt(0);
            }

            public void ExecuteNewestCallback ()
            {
                ExecuteCallbackAt(PendingCallbackCount - 1);
            }

            private void ExecuteCallbackAt (int index)
            {
                (SendOrPostCallback Callback, object State) callback;
                lock (syncRoot)
                {
                    callback = callbacks[index];
                    callbacks.RemoveAt(index);
                    if (callbacks.Count == 0)
                    {
                        callbackAvailable.Reset();
                    }
                }

                var originalSynchronizationContext = Current;
                try
                {
                    SetSynchronizationContext(this);
                    callback.Callback(callback.State);
                }
                finally
                {
                    SetSynchronizationContext(originalSynchronizationContext);
                }
            }
        }

        private sealed class MalformedReadBlockingWriteStream : BlockingWriteMemoryStream
        {
            public MalformedReadBlockingWriteStream ()
                : base(Array.Empty<byte>())
            {
            }

            public override int Read (
                byte[] buffer,
                int offset,
                int count)
            {
                throw new InvalidDataException("malformed request frame");
            }

            public override Task<int> ReadAsync (
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                return Task.FromException<int>(new InvalidDataException("malformed request frame"));
            }
        }

        private sealed class StubMethodDispatcher : IUnityIpcMethodDispatcher
        {
            public int DispatchCallCount { get; private set; }

            public int StreamingDispatchCallCount { get; private set; }

            public UnityIpcMethod? LastMethod { get; private set; }

            public Task<IpcResponse> DispatchAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestPhaseScope phaseScope)
            {
                phaseScope.ExecutionCancellation.Token.ThrowIfCancellationRequested();
                DispatchCallCount++;
                LastMethod = request.Method;
                return Task.FromResult(CreateResponse(request));
            }

            public Task<IpcResponse> DispatchStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestPhaseScope phaseScope)
            {
                phaseScope.ExecutionCancellation.Token.ThrowIfCancellationRequested();
                StreamingDispatchCallCount++;
                LastMethod = request.Method;
                return Task.FromResult(CreateResponse(request));
            }

            private static IpcResponse CreateResponse (
                ValidatedUnityIpcRequest request)
            {
                return new IpcResponse(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: request.RequestId,
                    status: IpcResponseStatus.Ok,
                    payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(true, "ok")),
                    errors: Array.Empty<IpcError>());
            }
        }

        private sealed class CancellationObservingRequestHandler : ValidatedUnityIpcRequestHandlerStub
        {
            private readonly TaskCompletionSource<bool> requestObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> cancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task RequestObserved => requestObserved.Task;

            public Task CancellationObserved => cancellationObserved.Task;

            public override async Task<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestPhaseScope phaseScope)
            {
                requestObserved.TrySetResult(true);
                var cancellationToken = phaseScope.ExecutionCancellation.Token;
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    throw new InvalidOperationException("Cancellation-observing request unexpectedly completed.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    cancellationObserved.TrySetResult(true);
                    throw;
                }
            }

            public override Task<IpcResponse> HandleStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestPhaseScope phaseScope)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class CompletableRequestHandler : ValidatedUnityIpcRequestHandlerStub
        {
            private readonly TaskCompletionSource<bool> requestObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> cancellationObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<IpcResponse> completionSource =
                new TaskCompletionSource<IpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> completionObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task RequestObserved => requestObserved.Task;

            public Task CancellationObserved => cancellationObserved.Task;

            public Task CompletionObserved => completionObserved.Task;

            public void Complete (IpcRequestEnvelope request)
            {
                completionSource.TrySetResult(new IpcResponse(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: request.RequestId,
                    status: IpcResponseStatus.Ok,
                    payload: JsonSerializer.SerializeToElement(new { }),
                    errors: Array.Empty<IpcError>()));
            }

            public override async Task<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestPhaseScope phaseScope)
            {
                requestObserved.TrySetResult(true);
                var cancellationToken = phaseScope.ExecutionCancellation.Token;
                using var cancellationRegistration = cancellationToken.Register(
                    static state => ((TaskCompletionSource<bool>)state).TrySetResult(true),
                    cancellationObserved);
                var response = await completionSource.Task;
                cancellationToken.ThrowIfCancellationRequested();
                completionObserved.TrySetResult(true);
                return response;
            }

            public override Task<IpcResponse> HandleStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestPhaseScope phaseScope)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class RecordingIpcRequestPhaseScopeFactory : IIpcRequestPhaseScopeFactory
        {
            private readonly IpcRequestPhaseScopeFactory inner = new IpcRequestPhaseScopeFactory();

            public bool CreateCalled { get; private set; }

            public CancellationToken UpstreamCancellationToken { get; private set; }

            public IpcRequestPhaseScope Create (
                IpcRequestEnvelope request,
                CancellationToken upstreamCancellationToken,
                TimeSpan maximumResponseFrameWriteDuration)
            {
                CreateCalled = true;
                UpstreamCancellationToken = upstreamCancellationToken;
                return inner.Create(
                    request,
                    upstreamCancellationToken,
                    maximumResponseFrameWriteDuration);
            }
        }

        private sealed class StubSessionTokenValidator : ISessionTokenValidator
        {
            private readonly bool result;

            public StubSessionTokenValidator (bool result)
            {
                this.result = result;
            }

            public int ValidateCallCount { get; private set; }

            public IpcSessionToken LastSessionToken { get; private set; }

            public Task<bool> ValidateAsync (
                IpcSessionToken sessionToken,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateCallCount++;
                LastSessionToken = sessionToken;
                return Task.FromResult(result);
            }
        }

        private sealed class RecordingDaemonLogger : IDaemonLogger
        {
            public int WarningCallCount { get; private set; }

            public string LastWarningMessage { get; private set; }

            public string LastWarningRaw { get; private set; }

            public void Info (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Warning (
                string category,
                string message,
                string raw = null)
            {
                WarningCallCount++;
                LastWarningMessage = message;
                LastWarningRaw = raw;
            }

            public void Error (
                string category,
                string message,
                string raw = null)
            {
            }

            public void Exception (
                string category,
                string message,
                Exception exception)
            {
            }
        }

        private sealed class NoOpStreamFrameWriter : IIpcStreamFrameWriter
        {
            public ValueTask WriteProgressAsync<TPayload> (
                string eventName,
                TPayload payload,
                CancellationToken cancellationToken = default)
                where TPayload : notnull
            {
                cancellationToken.ThrowIfCancellationRequested();
                return default;
            }

            public ValueTask WriteTerminalAsync (
                IpcResponse response,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return default;
            }
        }

        private sealed class StubStreamingRequestHandler : ValidatedUnityIpcRequestHandlerStub
        {
            public int CallCount { get; private set; }

            public int StreamingCallCount { get; private set; }

            public override Task<IpcResponse> HandleAsync (
                ValidatedUnityIpcRequest request,
                IpcRequestPhaseScope phaseScope)
            {
                CallCount++;
                return Task.FromResult(CreateResponse(request));
            }

            public override async Task<IpcResponse> HandleStreamingAsync (
                ValidatedUnityIpcRequest request,
                IIpcStreamFrameWriter streamWriter,
                IpcRequestPhaseScope phaseScope)
            {
                StreamingCallCount++;
                Assert.That(request.ResponseMode, Is.EqualTo(IpcResponseMode.Stream));
                await streamWriter.WriteProgressAsync(
                    "test.progress",
                    new UcliEmptyArgs(),
                    phaseScope.ExecutionCancellation.Token);
                return CreateResponse(request);
            }

            private static IpcResponse CreateResponse (ValidatedUnityIpcRequest request)
            {
                return new IpcResponse(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: request.RequestId,
                    status: IpcResponseStatus.Ok,
                    payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(true, "ok")),
                    errors: Array.Empty<IpcError>());
            }
        }

        private sealed class ThrowOnReadAndWriteStream : Stream
        {
            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => 0;

            public override long Position
            {
                get => 0;
                set => throw new NotSupportedException();
            }

            public override void Flush ()
            {
            }

            public override int Read (
                byte[] buffer,
                int offset,
                int count)
            {
                throw new InvalidDataException("read failed");
            }

            public override Task<int> ReadAsync (
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                return Task.FromException<int>(new InvalidDataException("read failed"));
            }

            public override long Seek (
                long offset,
                SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength (long value)
            {
                throw new NotSupportedException();
            }

            public override void Write (
                byte[] buffer,
                int offset,
                int count)
            {
                throw new IOException("write failed");
            }

            public override Task WriteAsync (
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                return Task.FromException(new IOException("write failed"));
            }
        }

        private sealed class ThrowOnWriteStream : Stream
        {
            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => 0;

            public override long Position
            {
                get => 0;
                set => throw new NotSupportedException();
            }

            public override void Flush ()
            {
            }

            public override int Read (
                byte[] buffer,
                int offset,
                int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek (
                long offset,
                SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength (long value)
            {
                throw new NotSupportedException();
            }

            public override void Write (
                byte[] buffer,
                int offset,
                int count)
            {
                throw new IOException("write failed");
            }

            public override Task WriteAsync (
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                return Task.FromException(new IOException("write failed"));
            }
        }

    }
}
