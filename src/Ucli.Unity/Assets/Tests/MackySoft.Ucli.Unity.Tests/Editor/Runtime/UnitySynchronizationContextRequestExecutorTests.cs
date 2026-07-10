using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnitySynchronizationContextRequestExecutorTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenCapturedSynchronizationContextDropsPostedWork_ProcessesQueueFromEditorUpdate ()
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            using var executor = new UnitySynchronizationContextRequestExecutor(
                new DroppingSynchronizationContext(),
                mainThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations,
                poisonOnActiveCancellation: true);

            var executionTask = Task.Run(() => executor.ExecuteAsync(
                () => Task.FromResult(Thread.CurrentThread.ManagedThreadId),
                CancellationToken.None));

            var deadlineUtc = DateTime.UtcNow.AddSeconds(5);
            while (!executionTask.IsCompleted && DateTime.UtcNow < deadlineUtc)
            {
                yield return null;
            }

            Assert.IsTrue(executionTask.IsCompleted, "Queued Unity main-thread work was not processed from the editor update pump.");
            Assert.IsFalse(executionTask.IsCanceled);
            if (executionTask.IsFaulted)
            {
                throw executionTask.Exception!;
            }

            Assert.AreEqual(mainThreadId, executionTask.Result);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenMutationAwaits_ReportsBusyUntilWorkCompletes () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            var release = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            var executionTask = executor.ExecuteAsync(async () =>
            {
                await release.Task;
                return true;
            });

            Assert.That(executor.IsBusy, Is.True);

            release.TrySetResult(null);
            await TestAwaiter.WaitAsync(
                executionTask.AsUniTask(),
                "Mutation executor completion",
                AsyncWaitTimeout);
            Assert.That(executor.IsBusy, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenPendingQueueIsFull_RejectsAdditionalWork () => UniTask.ToCoroutine(async () =>
        {
            using var executor = new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                maxPendingInvocations: 1,
                poisonOnActiveCancellation: true);
            var release = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstTask = executor.ExecuteAsync(async () =>
            {
                await release.Task;
                return 1;
            });
            var secondTask = executor.ExecuteAsync(() => Task.FromResult(2));

            await AsyncExceptionCapture.CaptureAsync<UnityMainThreadRequestQueueFullException>(async () =>
            {
                await executor.ExecuteAsync(() => Task.FromResult(3)).AsUniTask();
            }, "Full mutation request queue", AsyncWaitTimeout);

            release.TrySetResult(null);
            await TestAwaiter.WaitAsync(firstTask.AsUniTask(), "First queued mutation", AsyncWaitTimeout);
            await TestAwaiter.WaitAsync(secondTask.AsUniTask(), "Second queued mutation", AsyncWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenPendingMutationIsCanceled_ReclaimsQueueCapacity () => UniTask.ToCoroutine(async () =>
        {
            using var executor = new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                maxPendingInvocations: 1,
                poisonOnActiveCancellation: true);
            var release = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var activeTask = executor.ExecuteAsync(async () =>
            {
                await release.Task;
                return 1;
            });
            using var pendingCancellationSource = new CancellationTokenSource();
            var canceledWorkCalled = false;
            var canceledTask = executor.ExecuteAsync(() =>
            {
                canceledWorkCalled = true;
                return Task.FromResult(2);
            }, pendingCancellationSource.Token);

            pendingCancellationSource.Cancel();
            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await canceledTask.AsUniTask();
            }, "Canceled queued mutation", AsyncWaitTimeout);
            var replacementTask = executor.ExecuteAsync(() => Task.FromResult(3));

            Assert.That(replacementTask.IsFaulted, Is.False);
            Assert.That(canceledWorkCalled, Is.False);
            release.TrySetResult(null);
            await TestAwaiter.WaitAsync(activeTask.AsUniTask(), "Active mutation completion", AsyncWaitTimeout);
            Assert.That(await replacementTask, Is.EqualTo(3));
            Assert.That(canceledWorkCalled, Is.False);
        });

        [Test]
        [Category("Size.Small")]
        public void CanceledPendingInvocation_DoesNotKeepExecutorBusyOrBlockAdmissionSeal ()
        {
            using var executor = new UnitySynchronizationContextRequestExecutor(
                new DroppingSynchronizationContext(),
                mainThreadId: int.MinValue,
                maxPendingInvocations: 1,
                poisonOnActiveCancellation: true);
            using var cancellationTokenSource = new CancellationTokenSource();
            var canceledTask = executor.ExecuteAsync(
                () => Task.FromResult(1),
                cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            Assert.That(canceledTask.IsCanceled, Is.True);
            Assert.That(executor.IsBusy, Is.False);
            Assert.That(executor.TrySealAdmissionWhenIdle(out var admissionSeal), Is.True);
            admissionSeal.Dispose();
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenDispatchSynchronizationContextStops_CompletesInternalBookkeepingOffContext () => UniTask.ToCoroutine(async () =>
        {
            var originalSynchronizationContext = SynchronizationContext.Current;
            var dispatchSynchronizationContext = new DroppingSynchronizationContext();
            var workStarted = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseWork = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<int> executionTask;
            using var executor = new UnitySynchronizationContextRequestExecutor(
                dispatchSynchronizationContext,
                Thread.CurrentThread.ManagedThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations,
                poisonOnActiveCancellation: false);
            try
            {
                SynchronizationContext.SetSynchronizationContext(dispatchSynchronizationContext);
                executionTask = executor.ExecuteAsync(() => Task.Run(async () =>
                {
                    workStarted.TrySetResult(null);
                    await releaseWork.Task.ConfigureAwait(false);
                    return 42;
                }));
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(originalSynchronizationContext);
            }

            await TestAwaiter.WaitAsync(
                workStarted.Task.AsUniTask(),
                "Off-context mutation start",
                AsyncWaitTimeout);
            releaseWork.TrySetResult(null);

            var result = await TestAwaiter.WaitAsync(
                executionTask.AsUniTask(),
                "Off-context mutation completion",
                AsyncWaitTimeout);
            Assert.That(result, Is.EqualTo(42));
            Assert.That(executor.IsBusy, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Poison_WhenInvocationIsActive_RejectsPendingAndFutureMutations () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            var release = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var activeTask = executor.ExecuteAsync(async () =>
            {
                await release.Task;
                return 1;
            });
            var pendingTask = executor.ExecuteAsync(() => Task.FromResult(2));

            executor.Poison("Mutation did not quiesce after cancellation.");

            Assert.That(executor.IsPoisoned, Is.True);
            Assert.That(executor.IsBusy, Is.True);
            await AsyncExceptionCapture.CaptureAsync<UnityMutationLanePoisonedException>(async () =>
            {
                await pendingTask.AsUniTask();
            }, "Pending mutation after poison", AsyncWaitTimeout);
            await AsyncExceptionCapture.CaptureAsync<UnityMutationLanePoisonedException>(async () =>
            {
                await executor.ExecuteAsync(() => Task.FromResult(3)).AsUniTask();
            }, "Future mutation after poison", AsyncWaitTimeout);

            release.TrySetResult(null);
            await TestAwaiter.WaitAsync(activeTask.AsUniTask(), "Active mutation completion", AsyncWaitTimeout);
            Assert.That(executor.IsBusy, Is.True, "Poison must remain busy until the Editor host is replaced.");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenActiveMutationIgnoresCancellation_PoisonsBeforePublishingCancellation () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            using var cancellationTokenSource = new CancellationTokenSource();
            var release = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var workCompleted = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var executionTask = executor.ExecuteAsync(async () =>
            {
                await release.Task;
                workCompleted.TrySetResult(null);
                return 1;
            }, cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await executionTask.AsUniTask();
            }, "Canceled non-cooperative mutation", AsyncWaitTimeout);
            Assert.That(executor.IsPoisoned, Is.True);
            Assert.That(executor.IsBusy, Is.True);

            release.TrySetResult(null);
            await TestAwaiter.WaitAsync(
                workCompleted.Task.AsUniTask(),
                "Underlying canceled mutation completion",
                AsyncWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenActiveMutationCooperativelyCancels_DoesNotPoison () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationObserved = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationRegistration = cancellationTokenSource.Token.Register(
                static state => ((TaskCompletionSource<object>)state).TrySetResult(null),
                cancellationObserved);
            var executionTask = executor.ExecuteAsync(async () =>
            {
                await cancellationObserved.Task;
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                return 1;
            }, cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await executionTask.AsUniTask();
            }, "Cooperatively canceled mutation", AsyncWaitTimeout);
            Assert.That(executor.IsPoisoned, Is.False);
            Assert.That(executor.IsBusy, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenActiveControlWorkIgnoresCancellation_DetachesAndProcessesNextRequest () => UniTask.ToCoroutine(async () =>
        {
            using var executor = new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations,
                poisonOnActiveCancellation: false);
            using var cancellationTokenSource = new CancellationTokenSource();
            var release = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstTask = executor.ExecuteAsync(async () =>
            {
                await release.Task;
                return 1;
            }, cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();
            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await firstTask.AsUniTask();
            }, "Canceled non-cooperative control request", AsyncWaitTimeout);

            var secondResult = await TestAwaiter.WaitAsync(
                executor.ExecuteAsync(() => Task.FromResult(2)).AsUniTask(),
                "Control request after canceled non-cooperative request",
                AsyncWaitTimeout);

            Assert.That(secondResult, Is.EqualTo(2));
            Assert.That(executor.IsPoisoned, Is.False);
            Assert.That(executor.IsBusy, Is.False);
            release.TrySetResult(null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TrySealAdmissionWhenIdle_BlocksNewMutationsUntilSealIsReleased () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();

            Assert.That(executor.TrySealAdmissionWhenIdle(out var admissionSeal), Is.True);
            Assert.That(executor.IsBusy, Is.True);
            await AsyncExceptionCapture.CaptureAsync<UnityMutationLaneAdmissionSealedException>(async () =>
            {
                await executor.ExecuteAsync(() => Task.FromResult(1)).AsUniTask();
            }, "Mutation while admission is sealed", AsyncWaitTimeout);

            admissionSeal.Dispose();
            var result = await executor.ExecuteAsync(() => Task.FromResult(2));

            Assert.That(result, Is.EqualTo(2));
            Assert.That(executor.IsBusy, Is.False);
        });

        private static UnitySynchronizationContextRequestExecutor CreateMutationExecutor ()
        {
            return new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations,
                poisonOnActiveCancellation: true);
        }

        private sealed class DroppingSynchronizationContext : SynchronizationContext
        {
            public override void Post (
                SendOrPostCallback d,
                object state)
            {
            }
        }
    }
}
