using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class OneshotProcessLifetimeWatchdogTests
    {
        private static readonly AbsolutePath StorageRoot = AbsolutePath.Parse(
            Path.Combine(Path.GetTempPath(), "watchdog-storage-root"));

        private static readonly DateTimeOffset ObservedUtc =
            new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        private static readonly ProcessIdentity ParentProcess = new ProcessIdentity(42, 123);

        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [Test]
        [Category("Size.Small")]
        public void Timer_WhenParentProcessDisappears_ExitsFromWorkerThreadWhileMainThreadIsBlocked ()
        {
            var testThreadId = Thread.CurrentThread.ManagedThreadId;
            var exitObserved = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            ProcessIdentity observedParentProcess = null;
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc.AddMinutes(1)),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: parentProcess =>
                {
                    observedParentProcess = parentProcess;
                    return false;
                },
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () =>
                {
                    exitObserved.TrySetResult(Thread.CurrentThread.ManagedThreadId);
                });

            Assert.That(exitObserved.Task.Wait(SignalWaitTimeout), Is.True);
            Assert.That(exitObserved.Task.Result, Is.Not.EqualTo(testThreadId));
            Assert.That(observedParentProcess, Is.EqualTo(ParentProcess));
        }

        [Test]
        [Category("Size.Small")]
        public void Timer_WhenDeadlineHasNoRemainingTime_ExitsWhileMainThreadIsBlocked ()
        {
            var exitObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: static _ => true,
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () =>
                {
                    exitObserved.TrySetResult(true);
                });

            Assert.That(exitObserved.Task.Wait(SignalWaitTimeout), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Timer_WhileParentIsAliveAndDeadlineIsFuture_DoesNotExit ()
        {
            var secondProbeObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var probeCount = 0;
            var cleanupCount = 0;
            var exitCount = 0;
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc.AddMinutes(1)),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: _ =>
                {
                    if (Interlocked.Increment(ref probeCount) >= 2)
                    {
                        secondProbeObserved.TrySetResult(true);
                    }

                    return true;
                },
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: (_, _) => Interlocked.Increment(ref cleanupCount) == 1,
                terminateProcess: () => Interlocked.Increment(ref exitCount));

            Assert.That(secondProbeObserved.Task.Wait(SignalWaitTimeout), Is.True);
            watchdog.Dispose();

            Assert.That(Volatile.Read(ref cleanupCount), Is.EqualTo(0));
            Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(0));
        }

        [Test]
        [Category("Size.Small")]
        public void Timer_WhenMonotonicDeadlineIsReached_ExitsOnce ()
        {
            var elapsedTicks = 0L;
            var cleanupCount = 0;
            var exitCount = 0;
            var exitObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc.AddMinutes(1)),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: static _ => true,
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(
                    () => new TimeSpan(Interlocked.Read(ref elapsedTicks))),
                tryDeleteEnvelopeIfOwned: (_, _) => Interlocked.Increment(ref cleanupCount) == 1,
                terminateProcess: () =>
                {
                    Interlocked.Increment(ref exitCount);
                    exitObserved.TrySetResult(true);
                });

            Interlocked.Exchange(ref elapsedTicks, TimeSpan.FromMinutes(1).Ticks);

            Assert.That(exitObserved.Task.Wait(SignalWaitTimeout), Is.True);
            Assert.That(Volatile.Read(ref cleanupCount), Is.EqualTo(1));
            Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(1));
        }

        [Test]
        [Category("Size.Small")]
        public void Timer_WhenMonotonicClockHasEarlierOrigin_UsesElapsedDeltaFromConstruction ()
        {
            var clockOrigin = TimeSpan.FromMinutes(5);
            var requestExitTimeout = TimeSpan.FromMinutes(1);
            var elapsedTicks = clockOrigin.Ticks;
            var firstObservation = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var exitObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc + requestExitTimeout),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: _ =>
                {
                    firstObservation.TrySetResult("parent-probed");
                    return true;
                },
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(
                    () => new TimeSpan(Interlocked.Read(ref elapsedTicks))),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () =>
                {
                    firstObservation.TrySetResult("exit-requested");
                    exitObserved.TrySetResult(true);
                });

            Assert.That(firstObservation.Task.Wait(SignalWaitTimeout), Is.True);
            Assert.That(firstObservation.Task.Result, Is.EqualTo("parent-probed"));
            Assert.That(exitObserved.Task.IsCompleted, Is.False);

            Interlocked.Exchange(
                ref elapsedTicks,
                (clockOrigin + requestExitTimeout).Ticks);

            Assert.That(exitObserved.Task.Wait(SignalWaitTimeout), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void MarkRequestCompleted_WhenDeadlineLaterArrivesAndParentIsAlive_DoesNotExit ()
        {
            var requestExitTimeout = TimeSpan.FromMinutes(1);
            var elapsedTicks = 0L;
            var parentProbeAfterDeadline = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupCount = 0;
            var exitCount = 0;
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc + requestExitTimeout),
                pollInterval: PollInterval,
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
                tryDeleteEnvelopeIfOwned: (_, _) => Interlocked.Increment(ref cleanupCount) == 1,
                terminateProcess: () => Interlocked.Increment(ref exitCount));

            watchdog.MarkRequestCompleted();
            Interlocked.Exchange(ref elapsedTicks, requestExitTimeout.Ticks);

            Assert.That(parentProbeAfterDeadline.Task.Wait(SignalWaitTimeout), Is.True);
            watchdog.Dispose();
            Assert.That(Volatile.Read(ref cleanupCount), Is.EqualTo(0));
            Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(0));
        }

        [Test]
        [Category("Size.Small")]
        public void MarkRequestCompleted_WhileDeadlineInspectionIsInFlight_WhenCompletionWins_DoesNotExit ()
        {
            var elapsedTimeProbeEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var parentProbeObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseElapsedTimeProbe = new ManualResetEventSlim();
            var monotonicObservationCount = 0;
            var cleanupCount = 0;
            var exitCount = 0;
            var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: _ =>
                {
                    parentProbeObserved.TrySetResult(true);
                    return true;
                },
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(() =>
                {
                    if (Interlocked.Increment(ref monotonicObservationCount) > 1)
                    {
                        elapsedTimeProbeEntered.TrySetResult(true);
                        releaseElapsedTimeProbe.Wait();
                    }

                    return TimeSpan.Zero;
                }),
                tryDeleteEnvelopeIfOwned: (_, _) => Interlocked.Increment(ref cleanupCount) == 1,
                terminateProcess: () => Interlocked.Increment(ref exitCount));
            try
            {
                Assert.That(elapsedTimeProbeEntered.Task.Wait(SignalWaitTimeout), Is.True);

                watchdog.MarkRequestCompleted();
                releaseElapsedTimeProbe.Set();

                Assert.That(parentProbeObserved.Task.Wait(SignalWaitTimeout), Is.True);
                watchdog.Dispose();
                Assert.That(Volatile.Read(ref cleanupCount), Is.EqualTo(0));
                Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(0));
            }
            finally
            {
                releaseElapsedTimeProbe.Set();
                watchdog.Dispose();
            }
        }

        [Test]
        [Category("Size.Small")]
        public void MarkRequestCompleted_WhenParentLaterDisappears_StillExits ()
        {
            var parentIsAlive = 1;
            var exitObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc.AddMinutes(1)),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: _ => Volatile.Read(ref parentIsAlive) != 0,
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => exitObserved.TrySetResult(true));

            watchdog.MarkRequestCompleted();
            Volatile.Write(ref parentIsAlive, 0);

            Assert.That(exitObserved.Task.Wait(SignalWaitTimeout), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Dispose_WhileParentProbeIsInFlight_PreventsExitAfterParentDisappears ()
        {
            var probeEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var probeReturning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseProbe = new ManualResetEventSlim();
            var exitCount = 0;
            var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc.AddMinutes(1)),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: _ =>
                {
                    probeEntered.TrySetResult(true);
                    releaseProbe.Wait();
                    probeReturning.TrySetResult(true);
                    return false;
                },
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => Interlocked.Increment(ref exitCount));
            try
            {
                Assert.That(probeEntered.Task.Wait(SignalWaitTimeout), Is.True);

                watchdog.Dispose();
                releaseProbe.Set();
                Assert.That(probeReturning.Task.Wait(SignalWaitTimeout), Is.True);

                Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(0));
            }
            finally
            {
                releaseProbe.Set();
                watchdog.Dispose();
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Dispose_AfterRequestCompletedWhileParentProbeIsInFlight_PreventsExitAfterParentDisappears ()
        {
            var elapsedTimeProbeEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var parentProbeEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var parentProbeReturning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseElapsedTimeProbe = new ManualResetEventSlim();
            using var releaseParentProbe = new ManualResetEventSlim();
            var monotonicObservationCount = 0;
            var exitCount = 0;
            var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc.AddMinutes(1)),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: _ =>
                {
                    parentProbeEntered.TrySetResult(true);
                    releaseParentProbe.Wait();
                    parentProbeReturning.TrySetResult(true);
                    return false;
                },
                observedUtcNow: ObservedUtc,
                monotonicClock: new DelegatingMonotonicClock(() =>
                {
                    if (Interlocked.Increment(ref monotonicObservationCount) > 1)
                    {
                        elapsedTimeProbeEntered.TrySetResult(true);
                        releaseElapsedTimeProbe.Wait();
                    }

                    return TimeSpan.Zero;
                }),
                tryDeleteEnvelopeIfOwned: static (_, _) => true,
                terminateProcess: () => Interlocked.Increment(ref exitCount));
            try
            {
                Assert.That(elapsedTimeProbeEntered.Task.Wait(SignalWaitTimeout), Is.True);
                watchdog.MarkRequestCompleted();
                releaseElapsedTimeProbe.Set();
                Assert.That(parentProbeEntered.Task.Wait(SignalWaitTimeout), Is.True);

                watchdog.Dispose();
                releaseParentProbe.Set();
                Assert.That(parentProbeReturning.Task.Wait(SignalWaitTimeout), Is.True);

                Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(0));
            }
            finally
            {
                releaseElapsedTimeProbe.Set();
                releaseParentProbe.Set();
                watchdog.Dispose();
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Timer_WhenExitConditionRemainsTrue_InvokesExitOnlyOnce ()
        {
            var exitEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var exitCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseExit = new ManualResetEventSlim();
            var cleanupCount = 0;
            var exitCount = 0;
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc.AddMinutes(-1)),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: static _ => false,
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: (_, _) => Interlocked.Increment(ref cleanupCount) == 1,
                terminateProcess: () =>
                {
                    Interlocked.Increment(ref exitCount);
                    exitEntered.TrySetResult(true);
                    try
                    {
                        releaseExit.Wait();
                    }
                    finally
                    {
                        exitCompleted.TrySetResult(true);
                    }
                });
            try
            {
                Assert.That(exitEntered.Task.Wait(SignalWaitTimeout), Is.True);
                Thread.Sleep(TimeSpan.FromMilliseconds(100));

                Assert.That(Volatile.Read(ref cleanupCount), Is.EqualTo(1));
                Assert.That(Volatile.Read(ref exitCount), Is.EqualTo(1));
            }
            finally
            {
                releaseExit.Set();
                Assert.That(exitCompleted.Task.Wait(SignalWaitTimeout), Is.True);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Timer_WhenExitIsRequested_RequestsExactEnvelopeDeletionBeforeExiting ()
        {
            var expectedEnvelope = CreateBootstrapEnvelope(ObservedUtc);
            var exitObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            AbsolutePath observedStorageRoot = null;
            IpcOneshotBootstrapEnvelope observedEnvelope = null;
            var sequence = 0;
            var cleanupSequence = 0;
            var exitSequence = 0;
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: expectedEnvelope,
                pollInterval: PollInterval,
                parentProcessIsSameProcess: static _ => true,
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: (storageRoot, bootstrapEnvelope) =>
                {
                    observedStorageRoot = storageRoot;
                    observedEnvelope = bootstrapEnvelope;
                    cleanupSequence = Interlocked.Increment(ref sequence);
                    return true;
                },
                terminateProcess: () =>
                {
                    exitSequence = Interlocked.Increment(ref sequence);
                    exitObserved.TrySetResult(true);
                });

            Assert.That(exitObserved.Task.Wait(SignalWaitTimeout), Is.True);
            Assert.That(observedStorageRoot, Is.EqualTo(StorageRoot));
            Assert.That(observedEnvelope, Is.SameAs(expectedEnvelope));
            Assert.That(cleanupSequence, Is.EqualTo(1));
            Assert.That(exitSequence, Is.EqualTo(2));
        }

        [Test]
        [Category("Size.Small")]
        public void Timer_WhenEnvelopeCleanupFails_StillExits ()
        {
            var exitObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var watchdog = new OneshotProcessLifetimeWatchdog(
                storageRoot: StorageRoot,
                bootstrapEnvelope: CreateBootstrapEnvelope(ObservedUtc),
                pollInterval: PollInterval,
                parentProcessIsSameProcess: static _ => true,
                observedUtcNow: ObservedUtc,
                monotonicClock: new ManualMonotonicClock(),
                tryDeleteEnvelopeIfOwned: static (_, _) => throw new InvalidOperationException("cleanup failed"),
                terminateProcess: () => exitObserved.TrySetResult(true));

            Assert.That(exitObserved.Task.Wait(SignalWaitTimeout), Is.True);
        }

        private static IpcOneshotBootstrapEnvelope CreateBootstrapEnvelope (DateTimeOffset exitDeadlineUtc)
        {
            return new IpcOneshotBootstrapEnvelope(
                BootstrapId: Guid.Parse("c654b0e1-98ad-46b6-a6e5-1be351259fd0"),
                ParentProcess: ParentProcess,
                ProjectFingerprint: new ProjectFingerprint(
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
                SessionToken: IpcSessionToken.CreateRandom(),
                CreatedAtUtc: ObservedUtc.AddMinutes(-10),
                ExitDeadlineUtc: exitDeadlineUtc,
                Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-watchdog-tests"));
        }
    }
}
