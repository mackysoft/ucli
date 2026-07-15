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
    public sealed class UnityControlPlaneRequestExecutorTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenOneRequestAwaits_StartsFollowingRequestWithoutWaitingForCompletion () => UniTask.ToCoroutine(async () =>
        {
            using var executor = new UnityControlPlaneRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnityControlPlaneRequestExecutor.DefaultMaxConcurrentInvocations);
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstTask = executor.ExecuteAsync(async () =>
            {
                firstStarted.TrySetResult(true);
                await releaseFirst.Task;
                return 1;
            }, CancellationToken.None);

            await TestAwaiter.WaitAsync(
                firstStarted.Task.AsUniTask(),
                "First control-plane request start",
                AsyncWaitTimeout);
            var secondTask = executor.ExecuteAsync(
                () => Task.FromResult(2),
                CancellationToken.None);

            Assert.That(
                await TestAwaiter.WaitAsync(
                    secondTask.AsUniTask(),
                    "Second control-plane request while the first request is awaiting",
                    AsyncWaitTimeout),
                Is.EqualTo(2));
            Assert.That(firstTask.IsCompleted, Is.False);

            releaseFirst.TrySetResult(true);
            Assert.That(
                await TestAwaiter.WaitAsync(
                    firstTask.AsUniTask(),
                    "First control-plane request completion",
                    AsyncWaitTimeout),
                Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenSubmittedOffMainThread_StartsWorkOnEditorMainThread () => UniTask.ToCoroutine(async () =>
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            using var executor = new UnityControlPlaneRequestExecutor(
                new DroppingSynchronizationContext(),
                mainThreadId,
                UnityControlPlaneRequestExecutor.DefaultMaxConcurrentInvocations);

            var result = await TestAwaiter.WaitAsync(
                Task.Run(() => executor.ExecuteAsync(
                        () => Task.FromResult(Thread.CurrentThread.ManagedThreadId),
                        CancellationToken.None))
                    .AsUniTask(),
                "Control-plane request submitted off the Unity main thread",
                AsyncWaitTimeout);

            Assert.That(result, Is.EqualTo(mainThreadId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenActiveRequestIsCanceled_CompletesOutwardlyWithoutWaitingForWork () => UniTask.ToCoroutine(async () =>
        {
            using var executor = new UnityControlPlaneRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnityControlPlaneRequestExecutor.DefaultMaxConcurrentInvocations);
            using var cancellationSource = new CancellationTokenSource();
            var workStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseWork = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var workTerminated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var executionTask = executor.ExecuteAsync(async () =>
            {
                try
                {
                    workStarted.TrySetResult(true);
                    await releaseWork.Task;
                    return 1;
                }
                finally
                {
                    workTerminated.TrySetResult(true);
                }
            }, cancellationSource.Token);

            await TestAwaiter.WaitAsync(
                workStarted.Task.AsUniTask(),
                "Cancelable control-plane request start",
                AsyncWaitTimeout);
            cancellationSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await executionTask.AsUniTask();
            }, "Canceled control-plane request", AsyncWaitTimeout);

            var retirementTask = executor.WaitForRetirementAsync();
            Assert.That(executor.HasUnfinishedWork, Is.True);
            Assert.That(retirementTask.IsCompleted, Is.False);

            releaseWork.TrySetResult(true);
            await TestAwaiter.WaitAsync(
                workTerminated.Task.AsUniTask(),
                "Canceled control-plane work termination",
                AsyncWaitTimeout);
            await TestAwaiter.WaitAsync(
                retirementTask.AsUniTask(),
                "Canceled control-plane request retirement",
                AsyncWaitTimeout);
            Assert.That(executor.HasUnfinishedWork, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispose_WhenActiveWorkContinues_WaitsForActualWorkRetirement () => UniTask.ToCoroutine(async () =>
        {
            var executor = new UnityControlPlaneRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                UnityControlPlaneRequestExecutor.DefaultMaxConcurrentInvocations);
            var workStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseWork = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var executionTask = executor.ExecuteAsync(async () =>
            {
                workStarted.TrySetResult(true);
                await releaseWork.Task;
                return 1;
            }, CancellationToken.None);

            await TestAwaiter.WaitAsync(
                workStarted.Task.AsUniTask(),
                "Disposable control-plane request start",
                AsyncWaitTimeout);
            var retirementTask = executor.WaitForRetirementAsync();
            executor.Dispose();

            await AsyncExceptionCapture.CaptureAsync<ObjectDisposedException>(async () =>
            {
                await executionTask.AsUniTask();
            }, "Disposed control-plane request", AsyncWaitTimeout);
            Assert.That(executor.HasUnfinishedWork, Is.True);
            Assert.That(retirementTask.IsCompleted, Is.False);

            releaseWork.TrySetResult(true);
            await TestAwaiter.WaitAsync(
                retirementTask.AsUniTask(),
                "Disposed control-plane request retirement",
                AsyncWaitTimeout);
            Assert.That(executor.HasUnfinishedWork, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenCapacityIsExhausted_RejectsAdditionalRequest () => UniTask.ToCoroutine(async () =>
        {
            using var executor = new UnityControlPlaneRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                maxConcurrentInvocations: 1);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var activeTask = executor.ExecuteAsync(async () =>
            {
                await release.Task;
                return 1;
            }, CancellationToken.None);

            await AsyncExceptionCapture.CaptureAsync<UnityControlPlaneCapacityExceededException>(async () =>
            {
                await executor.ExecuteAsync(
                        () => Task.FromResult(2),
                        CancellationToken.None)
                    .AsUniTask();
            }, "Full control-plane executor", AsyncWaitTimeout);

            release.TrySetResult(true);
            Assert.That(
                await TestAwaiter.WaitAsync(
                    activeTask.AsUniTask(),
                    "Active control-plane request completion",
                    AsyncWaitTimeout),
                Is.EqualTo(1));
        });

        private sealed class DroppingSynchronizationContext : SynchronizationContext
        {
            public override void Post (SendOrPostCallback callback, object state)
            {
            }
        }
    }
}
