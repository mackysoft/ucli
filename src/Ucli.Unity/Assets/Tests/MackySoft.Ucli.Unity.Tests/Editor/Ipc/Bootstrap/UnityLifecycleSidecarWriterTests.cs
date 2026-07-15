using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityLifecycleSidecarWriterTests
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

        [Test]
        [Category("Size.Small")]
        public async Task InitializeAsync_WhileInitialWriteIsIncomplete_DoesNotCompleteOnUnityThread ()
        {
            var writeReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var persistence = new RecordingLifecycleSidecarPersistence(
                (_, _, _) => writeReleaseSource.Task);
            var writer = new UnityLifecycleSidecarWriter(persistence);
            var unityThreadId = Thread.CurrentThread.ManagedThreadId;

            var initializeTask = writer.InitializeAsync(
                CreateObservation(IpcEditorLifecycleState.Ready),
                CreateObservedAtUtc(0),
                CancellationToken.None);
            await WaitUntilAsync(() => persistence.WriteCount == 1, "initial sidecar write start");

            Assert.That(initializeTask.IsCompleted, Is.False);
            Assert.That(persistence.LastWriteThreadId, Is.Not.EqualTo(unityThreadId));

            writeReleaseSource.SetResult(true);
            await TestAwaiter.WaitAsync(initializeTask, "initial sidecar write", TestTimeout);
            await TestAwaiter.WaitAsync(
                writer.StopAsync(CancellationToken.None),
                "initialized sidecar writer stop",
                TestTimeout);
        }

        [Test]
        [Category("Size.Small")]
        public async Task TryEnqueue_WhileWriteIsInFlight_PersistsOnlyLatestPendingSnapshot ()
        {
            var inFlightWriteReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var persistence = new RecordingLifecycleSidecarPersistence(
                (writeCount, _, _) => writeCount == 2
                    ? inFlightWriteReleaseSource.Task
                    : Task.CompletedTask);
            var writer = await CreateInitializedWriterAsync(persistence);

            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.Compiling), CreateObservedAtUtc(1), out _),
                Is.True);
            await WaitUntilAsync(() => persistence.WriteCount == 2, "first refresh write start");
            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.Reimporting), CreateObservedAtUtc(2), out _),
                Is.True);
            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.DomainReloading), CreateObservedAtUtc(3), out var latestVersion),
                Is.True);

            inFlightWriteReleaseSource.SetResult(true);
            await TestAwaiter.WaitAsync(
                writer.FlushAsync(latestVersion, CancellationToken.None),
                "latest sidecar snapshot flush",
                TestTimeout);

            Assert.That(
                persistence.GetWrittenLifecycleStates(),
                Is.EqualTo(new[]
                {
                    IpcEditorLifecycleState.Ready,
                    IpcEditorLifecycleState.Compiling,
                    IpcEditorLifecycleState.DomainReloading,
                }));

            await TestAwaiter.WaitAsync(
                writer.StopAsync(CancellationToken.None),
                "coalescing sidecar writer stop",
                TestTimeout);
        }

        [Test]
        [Category("Size.Small")]
        public async Task TryEnqueueDomainReloadRecovery_PersistsLeaseWithRecoverySnapshot ()
        {
            var persistence = new RecordingLifecycleSidecarPersistence(
                (_, _, _) => Task.CompletedTask);
            var writer = await CreateInitializedWriterAsync(persistence);
            var observedAtUtc = CreateObservedAtUtc(1);
            var recoveryLease = new DaemonLifecycleRecoveryLease(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                observedAtUtc + DaemonLifecycleObservationTimings.DomainReloadRecoveryLeaseDuration);

            Assert.That(
                writer.TryEnqueueDomainReloadRecovery(
                    CreateObservation(IpcEditorLifecycleState.Recovering),
                    observedAtUtc,
                    recoveryLease,
                    out var version),
                Is.True);
            await TestAwaiter.WaitAsync(
                writer.FlushAsync(version, CancellationToken.None),
                "domain reload recovery sidecar flush",
                TestTimeout);

            Assert.That(persistence.GetWrittenRecoveryLease(1), Is.EqualTo(recoveryLease));

            await TestAwaiter.WaitAsync(
                writer.StopAsync(CancellationToken.None),
                "domain reload recovery sidecar writer stop",
                TestTimeout);
        }

        [Test]
        [Category("Size.Small")]
        public async Task MutationObserver_BeforeRequestExecution_WaitsUntilBusySnapshotIsDurable ()
        {
            var busyWriteReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var persistence = new RecordingLifecycleSidecarPersistence(
                (writeCount, _, _) => writeCount == 2
                    ? busyWriteReleaseSource.Task
                    : Task.CompletedTask);
            var writer = await CreateInitializedWriterAsync(persistence);
            var executionStartSource = new StubMutationRequestExecutionStartSource();
            using var observer = new UnityMutationLifecycleSidecarObserver(
                executionStartSource,
                new StaticAvailabilityObservationSource(
                    CreateObservation(IpcEditorLifecycleState.Busy)),
                writer,
                NoOpDaemonLogger.Instance);

            var executionStartTask = executionStartSource.RaiseRequestExecutionStartingAsync(
                CancellationToken.None);
            await WaitUntilAsync(() => persistence.WriteCount == 2, "mutation busy sidecar write start");

            Assert.That(writer.LastScheduledAtUtc, Is.Not.Null);
            Assert.That(executionStartTask.IsCompleted, Is.False);
            busyWriteReleaseSource.TrySetResult(true);
            await TestAwaiter.WaitAsync(
                executionStartTask,
                "mutation busy sidecar durability barrier",
                TestTimeout);
            Assert.That(
                persistence.GetWrittenLifecycleStates(),
                Is.EqualTo(new[]
                {
                    IpcEditorLifecycleState.Ready,
                    IpcEditorLifecycleState.Busy,
                }));

            await TestAwaiter.WaitAsync(
                writer.StopAsync(CancellationToken.None),
                "mutation-observing sidecar writer stop",
                TestTimeout);
        }

        [Test]
        [Category("Size.Small")]
        public async Task TryConsumeFailure_AcrossRetries_NotifiesOncePerFailureStreak ()
        {
            var shouldFail = 0;
            var persistence = new RecordingLifecycleSidecarPersistence(
                (_, _, _) => Volatile.Read(ref shouldFail) != 0
                    ? Task.FromException(new IOException("sidecar sharing violation"))
                    : Task.CompletedTask);
            var writer = await CreateInitializedWriterAsync(persistence);

            Volatile.Write(ref shouldFail, 1);
            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.CompileFailed), CreateObservedAtUtc(1), out _),
                Is.True);
            var firstFailure = await WaitForFailureAsync(writer);

            Assert.That(firstFailure, Does.Contain("sidecar sharing violation"));
            Assert.That(writer.TryConsumeFailure(out _), Is.False);

            Volatile.Write(ref shouldFail, 0);
            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.Ready), CreateObservedAtUtc(2), out var recoveredVersion),
                Is.True);
            await TestAwaiter.WaitAsync(
                writer.FlushAsync(recoveredVersion, CancellationToken.None),
                "recovered sidecar write",
                TestTimeout);

            Volatile.Write(ref shouldFail, 1);
            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.ShuttingDown), CreateObservedAtUtc(3), out _),
                Is.True);
            var secondFailure = await WaitForFailureAsync(writer);

            Assert.That(secondFailure, Does.Contain("sidecar sharing violation"));

            await TestAwaiter.WaitAsync(
                writer.StopAsync(CancellationToken.None),
                "failed sidecar writer stop",
                TestTimeout);
        }

        [Test]
        [Category("Size.Small")]
        public async Task StopAsync_WhileWriteIsInFlight_CancelsWriteBeforeRejectingSuccessorWork ()
        {
            var cancellationCallbackReleaseSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var writeCancellationSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationCallbackThreadId = 0;
            var persistence = new RecordingLifecycleSidecarPersistence((writeCount, _, cancellationToken) =>
            {
                if (writeCount == 1)
                {
                    return Task.CompletedTask;
                }

                cancellationToken.Register(() =>
                {
                    Volatile.Write(
                        ref cancellationCallbackThreadId,
                        Thread.CurrentThread.ManagedThreadId);
                    writeCancellationSource.TrySetResult(true);
                    cancellationCallbackReleaseSource.Task.GetAwaiter().GetResult();
                });
                return Task.Delay(Timeout.Infinite, cancellationToken);
            });
            var writer = await CreateInitializedWriterAsync(persistence);
            var unityThreadId = Thread.CurrentThread.ManagedThreadId;

            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.Busy), CreateObservedAtUtc(1), out _),
                Is.True);
            await WaitUntilAsync(() => persistence.WriteCount == 2, "in-flight sidecar write start");

            var stopTask = writer.StopAsync(CancellationToken.None);
            await TestAwaiter.WaitAsync(
                writeCancellationSource.Task,
                "in-flight sidecar write cancellation",
                TestTimeout);

            Assert.That(stopTask.IsCompleted, Is.False);
            Assert.That(Volatile.Read(ref cancellationCallbackThreadId), Is.Not.EqualTo(unityThreadId));

            cancellationCallbackReleaseSource.SetResult(true);
            await TestAwaiter.WaitAsync(
                stopTask,
                "in-flight sidecar writer stop",
                TestTimeout);

            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.Starting), CreateObservedAtUtc(2), out _),
                Is.False);
            Assert.That(persistence.WriteCount, Is.EqualTo(2));
        }

        [Test]
        [Category("Size.Small")]
        public async Task InvalidateAndStopAsync_DeletesPersistedSidecarAfterWriterStops ()
        {
            var persistence = new RecordingLifecycleSidecarPersistence(
                (_, _, _) => Task.CompletedTask);
            var writer = await CreateInitializedWriterAsync(persistence);

            await TestAwaiter.WaitAsync(
                writer.InvalidateAndStopAsync(CancellationToken.None),
                "sidecar invalidation and stop",
                TestTimeout);

            Assert.That(persistence.DeleteCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Size.Small")]
        public async Task InvalidateAndStopAsync_WhenWriteIgnoresCancellation_ContinuesOwnedCleanupAfterCallerDeadline ()
        {
            var inFlightWriteReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var persistence = new RecordingLifecycleSidecarPersistence(
                (writeCount, _, _) => writeCount == 1
                    ? Task.CompletedTask
                    : inFlightWriteReleaseSource.Task);
            var writer = await CreateInitializedWriterAsync(persistence);
            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.Busy), CreateObservedAtUtc(1), out _),
                Is.True);
            await WaitUntilAsync(() => persistence.WriteCount == 2, "non-cooperative sidecar write start");
            using var cleanupCancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            Exception observedException = null;
            try
            {
                await writer.InvalidateAndStopAsync(cleanupCancellationSource.Token);
            }
            catch (Exception exception)
            {
                observedException = exception;
            }

            Assert.That(observedException, Is.TypeOf<OperationCanceledException>());
            Assert.That(persistence.DeleteCount, Is.Zero);

            inFlightWriteReleaseSource.SetResult(true);
            await WaitUntilAsync(
                () => persistence.DeleteCount == 1,
                "late owned sidecar invalidation");

            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.Starting), CreateObservedAtUtc(2), out _),
                Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public async Task TryConsumeFailure_WhenBackgroundRetryRecoversBeforeConsumption_DoesNotReportStaleFailure ()
        {
            var shouldFail = 0;
            var persistence = new RecordingLifecycleSidecarPersistence(
                (_, _, _) => Volatile.Read(ref shouldFail) != 0
                    ? Task.FromException(new IOException("transient sidecar failure"))
                    : Task.CompletedTask);
            var writer = await CreateInitializedWriterAsync(persistence);

            Volatile.Write(ref shouldFail, 1);
            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.CompileFailed), CreateObservedAtUtc(1), out _),
                Is.True);
            await WaitUntilAsync(() => persistence.WriteCount >= 2, "transient sidecar failure");

            Volatile.Write(ref shouldFail, 0);
            Assert.That(
                writer.TryEnqueue(CreateObservation(IpcEditorLifecycleState.Ready), CreateObservedAtUtc(2), out var recoveredVersion),
                Is.True);
            await TestAwaiter.WaitAsync(
                writer.FlushAsync(recoveredVersion, CancellationToken.None),
                "transient sidecar recovery",
                TestTimeout);

            Assert.That(writer.TryConsumeFailure(out _), Is.False);

            await TestAwaiter.WaitAsync(
                writer.StopAsync(CancellationToken.None),
                "recovered sidecar writer stop",
                TestTimeout);
        }

        private static async Task<UnityLifecycleSidecarWriter> CreateInitializedWriterAsync (
            RecordingLifecycleSidecarPersistence persistence)
        {
            var writer = new UnityLifecycleSidecarWriter(persistence);
            await TestAwaiter.WaitAsync(
                writer.InitializeAsync(
                    CreateObservation(IpcEditorLifecycleState.Ready),
                    CreateObservedAtUtc(0),
                    CancellationToken.None),
                "sidecar writer initialization",
                TestTimeout);
            return writer;
        }

        private static UnityEditorObservation CreateObservation (IpcEditorLifecycleState lifecycleState)
        {
            var compileState = lifecycleState switch
            {
                IpcEditorLifecycleState.Compiling => IpcCompileState.Compiling,
                IpcEditorLifecycleState.CompileFailed => IpcCompileState.Failed,
                _ => IpcCompileState.Ready,
            };
            return new UnityEditorObservation(
                state: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Gui,
                    lifecycleState: lifecycleState,
                    compileState: compileState,
                    generations: new IpcUnityGenerationSnapshot(1, 2, 0, 0),
                    playMode: new IpcPlayModeSnapshot(
                        IpcPlayModeState.Stopped,
                        IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedAtUtc: CreateObservedAtUtc(0));
        }

        private static DateTimeOffset CreateObservedAtUtc (int offsetSeconds)
        {
            return new DateTimeOffset(2026, 7, 11, 0, 0, offsetSeconds, TimeSpan.Zero);
        }

        private static async Task<string> WaitForFailureAsync (UnityLifecycleSidecarWriter writer)
        {
            string failure = null;
            await WaitUntilAsync(
                () => writer.TryConsumeFailure(out failure),
                "sidecar writer failure notification");
            return failure;
        }

        private static async Task WaitUntilAsync (Func<bool> predicate, string description)
        {
            var deadline = DateTimeOffset.UtcNow + TestTimeout;
            while (!predicate())
            {
                Assert.That(
                    DateTimeOffset.UtcNow,
                    Is.LessThan(deadline),
                    $"{description} did not complete within {TestTimeout}.");
                await Task.Delay(10);
            }
        }

        private sealed class RecordingLifecycleSidecarPersistence : IUnityLifecycleSidecarPersistence
        {
            private readonly object syncRoot = new object();

            private readonly Func<int, UnityEditorObservation, CancellationToken, Task> write;

            private readonly List<UnityEditorObservation> writtenSnapshots =
                new List<UnityEditorObservation>();

            private readonly List<DaemonLifecycleRecoveryLease> writtenRecoveryLeases =
                new List<DaemonLifecycleRecoveryLease>();

            private int deleteCount;

            private int lastWriteThreadId;

            public RecordingLifecycleSidecarPersistence (
                Func<int, UnityEditorObservation, CancellationToken, Task> write)
            {
                this.write = write ?? throw new ArgumentNullException(nameof(write));
            }

            public int WriteCount
            {
                get
                {
                    lock (syncRoot)
                    {
                        return writtenSnapshots.Count;
                    }
                }
            }

            public int DeleteCount => Volatile.Read(ref deleteCount);

            public int LastWriteThreadId => Volatile.Read(ref lastWriteThreadId);

            public Task WriteAsync (
                UnityEditorObservation snapshot,
                DaemonLifecycleRecoveryLease recoveryLease,
                CancellationToken cancellationToken)
            {
                int writeCount;
                lock (syncRoot)
                {
                    writtenSnapshots.Add(snapshot);
                    writtenRecoveryLeases.Add(recoveryLease);
                    writeCount = writtenSnapshots.Count;
                }

                Volatile.Write(ref lastWriteThreadId, Thread.CurrentThread.ManagedThreadId);
                return write(writeCount, snapshot, cancellationToken);
            }

            public DaemonLifecycleRecoveryLease GetWrittenRecoveryLease (int index)
            {
                lock (syncRoot)
                {
                    return writtenRecoveryLeases[index];
                }
            }

            public Task DeleteIfOwnedAsync (CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref deleteCount);
                return Task.CompletedTask;
            }

            public IReadOnlyList<IpcEditorLifecycleState> GetWrittenLifecycleStates ()
            {
                lock (syncRoot)
                {
                    var states = new IpcEditorLifecycleState[writtenSnapshots.Count];
                    for (var index = 0; index < writtenSnapshots.Count; index++)
                    {
                        states[index] = writtenSnapshots[index].State.LifecycleState;
                    }

                    return states;
                }
            }
        }

        private sealed class StubMutationRequestExecutionStartSource : IUnityMutationRequestExecutionStartSource
        {
            public event Func<CancellationToken, Task> RequestExecutionStarting;

            public Task RaiseRequestExecutionStartingAsync (CancellationToken cancellationToken)
            {
                var handler = RequestExecutionStarting;
                return handler == null
                    ? Task.CompletedTask
                    : handler(cancellationToken);
            }
        }

        private sealed class StaticAvailabilityObservationSource : IUnityEditorAvailabilityObservationSource
        {
            private readonly UnityEditorObservation observation;

            public StaticAvailabilityObservationSource (UnityEditorObservation observation)
            {
                this.observation = observation ?? throw new ArgumentNullException(nameof(observation));
            }

            public UnityEditorObservation CaptureAvailabilityObservation ()
            {
                return observation;
            }
        }
    }
}
