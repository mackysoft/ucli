using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class IpcRequestPhaseScopeTests
    {
        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [Test]
        [Category("Size.Small")]
        public void Create_WhenDeadlineIsExpired_ReturnsImmediateZeroCutoffs ()
        {
            var observedAtUtc = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

            var plan = IpcRequestPhasePlan.Create(
                CreateRequest(observedAtUtc, requestDeadlineRemainingMilliseconds: 1000),
                observedAtUtc,
                maximumResponseFrameWriteDuration: TimeSpan.FromSeconds(1));

            Assert.That(plan.ExecutionCutoff, Is.EqualTo(TimeSpan.Zero));
            Assert.That(plan.PersistenceCutoff, Is.EqualTo(TimeSpan.Zero));
            Assert.That(plan.WriteCutoff, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        [Category("Size.Small")]
        public void Create_WhenAbsoluteDeadlineHasHalfMillisecondRemaining_DoesNotExtendAnyCutoff ()
        {
            var observedAtUtc = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
            var absoluteRemaining = TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond / 2);

            var plan = IpcRequestPhasePlan.Create(
                CreateRequest(
                    observedAtUtc + absoluteRemaining,
                    requestDeadlineRemainingMilliseconds: 1000),
                observedAtUtc,
                maximumResponseFrameWriteDuration: TimeSpan.FromSeconds(1));

            Assert.That(plan.ExecutionCutoff, Is.EqualTo(absoluteRemaining));
            Assert.That(plan.PersistenceCutoff, Is.EqualTo(absoluteRemaining));
            Assert.That(plan.WriteCutoff, Is.EqualTo(absoluteRemaining));
        }

        [Test]
        [Category("Size.Small")]
        public void Create_WhenAbsoluteDeadlineIsFarInFuture_ClampsPlanToRequestDeadlineRemainingSnapshot ()
        {
            var observedAtUtc = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

            var plan = IpcRequestPhasePlan.Create(
                CreateRequest(
                    observedAtUtc + TimeSpan.FromDays(365),
                    requestDeadlineRemainingMilliseconds: 1000),
                observedAtUtc,
                maximumResponseFrameWriteDuration: TimeSpan.FromMilliseconds(100));

            Assert.That(plan.WriteCutoff, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(1)));
        }

        [Test]
        [Category("Size.Small")]
        public void Create_WhenResponseWriteDurationExceedsTimerLimit_ThrowsArgumentOutOfRangeException ()
        {
            var observedAtUtc = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => IpcRequestPhasePlan.Create(
                CreateRequest(
                    observedAtUtc + TimeSpan.FromSeconds(1),
                    requestDeadlineRemainingMilliseconds: 1000),
                observedAtUtc,
                IpcRequestPhasePlan.MaximumTimerDuration + TimeSpan.FromMilliseconds(1)));

            Assert.That(exception.ParamName, Is.EqualTo("maximumResponseFrameWriteDuration"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenMonotonicClockIsStopped_ThrowsArgumentException ()
        {
            var observedAtUtc = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
            var plan = IpcRequestPhasePlan.Create(
                CreateRequest(
                    observedAtUtc + TimeSpan.FromSeconds(1),
                    requestDeadlineRemainingMilliseconds: 1000),
                observedAtUtc,
                maximumResponseFrameWriteDuration: TimeSpan.FromMilliseconds(100));

            var exception = Assert.Throws<ArgumentException>(() =>
                _ = new IpcRequestPhaseScope(
                    plan,
                    new Stopwatch(),
                    CancellationToken.None));

            Assert.That(exception.ParamName, Is.EqualTo("elapsedTime"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PhaseScope_WhenCutoffsElapse_CancelsTokensInPlanOrder () => UniTask.ToCoroutine(async () =>
        {
            var observedOrder = new List<int>();
            var elapsedTime = Stopwatch.StartNew();
            var requestDeadlineUtc = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(50);
            var plan = IpcRequestPhasePlan.Create(
                CreateRequest(requestDeadlineUtc, requestDeadlineRemainingMilliseconds: 50),
                DateTimeOffset.UtcNow,
                maximumResponseFrameWriteDuration: TimeSpan.FromMilliseconds(10));
            using var phaseScope = new IpcRequestPhaseScope(
                plan,
                elapsedTime,
                CancellationToken.None);
            using var executionRegistration = phaseScope.ExecutionCancellation.Token.Register(
                () => observedOrder.Add(0));
            using var persistenceRegistration = phaseScope.PersistenceCutoffToken.Register(
                () => observedOrder.Add(1));
            using var writeRegistration = phaseScope.WriteCutoffToken.Register(
                () => observedOrder.Add(2));

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await Task.Delay(Timeout.Infinite, phaseScope.WriteCutoffToken);
            }, "request write cutoff", SignalWaitTimeout);

            Assert.That(observedOrder, Is.EqualTo(new[] { 0, 1, 2 }));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PhaseScope_WhenExecutionCancellationCallbackThrows_StillAdvancesLaterCutoffs () => UniTask.ToCoroutine(async () =>
        {
            var elapsedTime = Stopwatch.StartNew();
            var requestDeadlineUtc = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(50);
            var plan = IpcRequestPhasePlan.Create(
                CreateRequest(requestDeadlineUtc, requestDeadlineRemainingMilliseconds: 50),
                DateTimeOffset.UtcNow,
                maximumResponseFrameWriteDuration: TimeSpan.FromMilliseconds(10));
            using var phaseScope = new IpcRequestPhaseScope(
                plan,
                elapsedTime,
                CancellationToken.None);
            using var throwingRegistration = phaseScope.ExecutionCancellation.Token.Register(
                static () => throw new InvalidOperationException("test cancellation callback failure"));

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await Task.Delay(Timeout.Infinite, phaseScope.WriteCutoffToken);
            }, "request write cutoff after callback failure", SignalWaitTimeout);

            Assert.That(phaseScope.PersistenceCutoffToken.IsCancellationRequested, Is.True);
            Assert.That(phaseScope.WriteCutoffToken.IsCancellationRequested, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PhaseScope_WhenExecutionCancellationCallbackBlocks_StillAdvancesLaterCutoffs () => UniTask.ToCoroutine(async () =>
        {
            var callbackEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseCallback = new ManualResetEventSlim();
            var elapsedTime = Stopwatch.StartNew();
            var requestDeadlineUtc = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(200);
            var plan = IpcRequestPhasePlan.Create(
                CreateRequest(requestDeadlineUtc, requestDeadlineRemainingMilliseconds: 200),
                DateTimeOffset.UtcNow,
                maximumResponseFrameWriteDuration: TimeSpan.FromMilliseconds(20));
            using var phaseScope = new IpcRequestPhaseScope(
                plan,
                elapsedTime,
                CancellationToken.None);
            using var blockingRegistration = phaseScope.ExecutionCancellation.Token.Register(() =>
            {
                callbackEntered.TrySetResult(true);
                releaseCallback.Wait();
            });

            try
            {
                await callbackEntered.Task;
                await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
                {
                    await Task.Delay(Timeout.Infinite, phaseScope.WriteCutoffToken);
                }, "request write cutoff after blocking callback", SignalWaitTimeout);

                Assert.That(phaseScope.PersistenceCutoffToken.IsCancellationRequested, Is.True);
                Assert.That(phaseScope.WriteCutoffToken.IsCancellationRequested, Is.True);
            }
            finally
            {
                releaseCallback.Set();
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PhaseScope_WhenDisposedDuringBlockingExecutionCallback_DefersOwnedResourceDisposalWithoutWaiting () => UniTask.ToCoroutine(async () =>
        {
            var callbackEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseCallback = new ManualResetEventSlim();
            var elapsedTime = Stopwatch.StartNew();
            var requestDeadlineUtc = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(200);
            var plan = IpcRequestPhasePlan.Create(
                CreateRequest(requestDeadlineUtc, requestDeadlineRemainingMilliseconds: 200),
                DateTimeOffset.UtcNow,
                maximumResponseFrameWriteDuration: TimeSpan.FromMilliseconds(20));
            var phaseScope = new IpcRequestPhaseScope(
                plan,
                elapsedTime,
                CancellationToken.None);
            Exception callbackFailure = null;
            using var blockingRegistration = phaseScope.ExecutionCancellation.Token.Register(() =>
            {
                callbackEntered.TrySetResult(true);
                releaseCallback.Wait();
                try
                {
                    _ = phaseScope.PersistenceCutoffToken;
                }
                catch (Exception exception)
                {
                    callbackFailure = exception;
                }
                finally
                {
                    callbackCompleted.TrySetResult(true);
                }
            });

            try
            {
                await callbackEntered.Task;
                var disposeTask = Task.Run(() => phaseScope.Dispose());
                var completedTask = await Task.WhenAny(
                    disposeTask,
                    Task.Delay(SignalWaitTimeout));

                Assert.That(completedTask, Is.SameAs(disposeTask),
                    "Phase scope disposal must not wait for an external cancellation callback.");
                await disposeTask;

                releaseCallback.Set();
                await callbackCompleted.Task;

                Assert.That(callbackFailure, Is.Null,
                    "Owned cancellation resources must remain alive until the active timer callback exits.");
            }
            finally
            {
                releaseCallback.Set();
                phaseScope.Dispose();
            }
        });

        [Test]
        [Category("Size.Small")]
        public void CancellationReason_WhenDeadlineCancelsFirst_RemainsDeadlineAfterUpstreamCancellation ()
        {
            using var upstreamCancellationTokenSource = new CancellationTokenSource();
            using var deadlineCancellationTokenSource = new CancellationTokenSource();
            using var executionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                upstreamCancellationTokenSource.Token,
                deadlineCancellationTokenSource.Token);
            using var cancellation = new IpcRequestCancellation(
                executionCancellationTokenSource.Token,
                deadlineCancellationTokenSource.Token,
                upstreamCancellationTokenSource.Token);

            deadlineCancellationTokenSource.Cancel();
            upstreamCancellationTokenSource.Cancel();

            Assert.That(cancellation.Reason, Is.EqualTo(IpcRequestCancellationReason.ExecutionDeadline));
        }

        [Test]
        [Category("Size.Small")]
        public void CancellationReason_WhenUpstreamCancelsFirst_RemainsUpstreamAfterDeadlineCancellation ()
        {
            using var upstreamCancellationTokenSource = new CancellationTokenSource();
            using var deadlineCancellationTokenSource = new CancellationTokenSource();
            using var executionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                upstreamCancellationTokenSource.Token,
                deadlineCancellationTokenSource.Token);
            using var cancellation = new IpcRequestCancellation(
                executionCancellationTokenSource.Token,
                deadlineCancellationTokenSource.Token,
                upstreamCancellationTokenSource.Token);

            upstreamCancellationTokenSource.Cancel();
            deadlineCancellationTokenSource.Cancel();

            Assert.That(cancellation.Reason, Is.EqualTo(IpcRequestCancellationReason.Upstream));
        }

        private static IpcRequestEnvelope CreateRequest (
            DateTimeOffset requestDeadlineUtc,
            int requestDeadlineRemainingMilliseconds)
        {
            return new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "session-token",
                method: "ping",
                payload: default,
                responseMode: "single",
                requestDeadlineUtc: requestDeadlineUtc,
                requestDeadlineRemainingMilliseconds: requestDeadlineRemainingMilliseconds);
        }
    }
}
