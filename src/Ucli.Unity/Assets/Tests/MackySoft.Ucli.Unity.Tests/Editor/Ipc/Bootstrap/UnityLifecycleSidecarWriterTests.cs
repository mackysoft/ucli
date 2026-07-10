using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
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
                CreateSnapshot("initial"),
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
                writer.TryEnqueue(CreateSnapshot("first"), CreateObservedAtUtc(1), out _),
                Is.True);
            await WaitUntilAsync(() => persistence.WriteCount == 2, "first refresh write start");
            Assert.That(
                writer.TryEnqueue(CreateSnapshot("superseded"), CreateObservedAtUtc(2), out _),
                Is.True);
            Assert.That(
                writer.TryEnqueue(CreateSnapshot("latest"), CreateObservedAtUtc(3), out var latestVersion),
                Is.True);

            inFlightWriteReleaseSource.SetResult(true);
            await TestAwaiter.WaitAsync(
                writer.FlushAsync(latestVersion, CancellationToken.None),
                "latest sidecar snapshot flush",
                TestTimeout);

            Assert.That(
                persistence.GetWrittenLifecycleStates(),
                Is.EqualTo(new[] { "initial", "first", "latest" }));

            await TestAwaiter.WaitAsync(
                writer.StopAsync(CancellationToken.None),
                "coalescing sidecar writer stop",
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
                writer.TryEnqueue(CreateSnapshot("failing"), CreateObservedAtUtc(1), out _),
                Is.True);
            var firstFailure = await WaitForFailureAsync(writer);

            Assert.That(firstFailure, Does.Contain("sidecar sharing violation"));
            Assert.That(writer.TryConsumeFailure(out _), Is.False);

            Volatile.Write(ref shouldFail, 0);
            Assert.That(
                writer.TryEnqueue(CreateSnapshot("recovered"), CreateObservedAtUtc(2), out var recoveredVersion),
                Is.True);
            await TestAwaiter.WaitAsync(
                writer.FlushAsync(recoveredVersion, CancellationToken.None),
                "recovered sidecar write",
                TestTimeout);

            Volatile.Write(ref shouldFail, 1);
            Assert.That(
                writer.TryEnqueue(CreateSnapshot("failing-again"), CreateObservedAtUtc(3), out _),
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
                writer.TryEnqueue(CreateSnapshot("in-flight"), CreateObservedAtUtc(1), out _),
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
                writer.TryEnqueue(CreateSnapshot("successor"), CreateObservedAtUtc(2), out _),
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
                writer.TryEnqueue(CreateSnapshot("non-cooperative"), CreateObservedAtUtc(1), out _),
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
                writer.TryEnqueue(CreateSnapshot("successor"), CreateObservedAtUtc(2), out _),
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
                writer.TryEnqueue(CreateSnapshot("transient-failure"), CreateObservedAtUtc(1), out _),
                Is.True);
            await WaitUntilAsync(() => persistence.WriteCount >= 2, "transient sidecar failure");

            Volatile.Write(ref shouldFail, 0);
            Assert.That(
                writer.TryEnqueue(CreateSnapshot("recovered"), CreateObservedAtUtc(2), out var recoveredVersion),
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
                    CreateSnapshot("initial"),
                    CreateObservedAtUtc(0),
                    CancellationToken.None),
                "sidecar writer initialization",
                TestTimeout);
            return writer;
        }

        private static UnityEditorLifecycleSnapshot CreateSnapshot (string lifecycleState)
        {
            return new UnityEditorLifecycleSnapshot(
                DaemonEditorMode.Gui,
                lifecycleState,
                null,
                IpcCompileStateCodec.Ready,
                CompileGeneration: "compile-generation",
                DomainReloadGeneration: "reload-generation",
                CanAcceptExecutionRequests: true,
                ObservedAtUtc: null);
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

            private readonly Func<int, UnityEditorLifecycleSnapshot, CancellationToken, Task> write;

            private readonly List<UnityEditorLifecycleSnapshot> writtenSnapshots =
                new List<UnityEditorLifecycleSnapshot>();

            private int deleteCount;

            private int lastWriteThreadId;

            public RecordingLifecycleSidecarPersistence (
                Func<int, UnityEditorLifecycleSnapshot, CancellationToken, Task> write)
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
                UnityEditorLifecycleSnapshot snapshot,
                CancellationToken cancellationToken)
            {
                int writeCount;
                lock (syncRoot)
                {
                    writtenSnapshots.Add(snapshot);
                    writeCount = writtenSnapshots.Count;
                }

                Volatile.Write(ref lastWriteThreadId, Thread.CurrentThread.ManagedThreadId);
                return write(writeCount, snapshot, cancellationToken);
            }

            public Task DeleteIfOwnedAsync (CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref deleteCount);
                return Task.CompletedTask;
            }

            public IReadOnlyList<string> GetWrittenLifecycleStates ()
            {
                lock (syncRoot)
                {
                    var states = new string[writtenSnapshots.Count];
                    for (var index = 0; index < writtenSnapshots.Count; index++)
                    {
                        states[index] = writtenSnapshots[index].LifecycleState;
                    }

                    return states;
                }
            }
        }
    }
}
