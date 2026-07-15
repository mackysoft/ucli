using System;
using System.Collections;
using System.Diagnostics;
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

        [Test]
        [Category("Size.Small")]
        public void TypeContract_DoesNotImplementControlPlaneExecutor ()
        {
            Assert.That(
                typeof(IUnityControlPlaneRequestExecutor).IsAssignableFrom(
                    typeof(UnitySynchronizationContextRequestExecutor)),
                Is.False);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenCapturedSynchronizationContextDropsPostedWork_ProcessesQueueFromEditorUpdate ()
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            using var executor = new UnitySynchronizationContextRequestExecutor(
                new DroppingSynchronizationContext(),
                mainThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations);

            var executionTask = Task.Run(() => executor.ExecuteAsync(
                () => Task.FromResult(Thread.CurrentThread.ManagedThreadId),
                CancellationToken.None));

            var waitElapsedTime = Stopwatch.StartNew();
            while (!executionTask.IsCompleted && waitElapsedTime.Elapsed < AsyncWaitTimeout)
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
        public IEnumerator ExecuteAsync_BeforeWorkItemStarts_AwaitsExecutionStartBarrier () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            var barrierStartedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var barrierReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var executionStartCount = 0;
            var observerSawBusyLane = false;
            var workItemSawStartNotification = false;
            executor.RequestExecutionStarting += async _ =>
            {
                executionStartCount++;
                observerSawBusyLane = executor.IsBusy;
                barrierStartedSource.TrySetResult(true);
                await barrierReleaseSource.Task;
            };

            var executionTask = executor.ExecuteAsync(() =>
            {
                workItemSawStartNotification = executionStartCount == 1;
                return Task.FromResult(42);
            });
            await TestAwaiter.WaitAsync(
                barrierStartedSource.Task.AsUniTask(),
                "mutation execution-start barrier",
                AsyncWaitTimeout);

            Assert.That(workItemSawStartNotification, Is.False);
            barrierReleaseSource.TrySetResult(true);
            var result = await TestAwaiter.WaitAsync(
                executionTask.AsUniTask(),
                "mutation request after execution-start barrier",
                AsyncWaitTimeout);

            Assert.That(result, Is.EqualTo(42));
            Assert.That(executionStartCount, Is.EqualTo(1));
            Assert.That(observerSawBusyLane, Is.True);
            Assert.That(workItemSawStartNotification, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenExecutionStartContextStops_StartsWorkFromEditorUpdate () => UniTask.ToCoroutine(async () =>
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var originalSynchronizationContext = SynchronizationContext.Current;
            var droppingSynchronizationContext = new DroppingSynchronizationContext();
            var barrierStartedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var barrierReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var executor = new UnitySynchronizationContextRequestExecutor(
                droppingSynchronizationContext,
                mainThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations);
            executor.RequestExecutionStarting += async _ =>
            {
                barrierStartedSource.TrySetResult(true);
                await barrierReleaseSource.Task.ConfigureAwait(false);
            };

            Task<int> executionTask;
            try
            {
                SynchronizationContext.SetSynchronizationContext(droppingSynchronizationContext);
                executionTask = executor.ExecuteAsync(
                    () => Task.FromResult(Thread.CurrentThread.ManagedThreadId));
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(originalSynchronizationContext);
            }

            await TestAwaiter.WaitAsync(
                barrierStartedSource.Task.AsUniTask(),
                "mutation execution-start barrier on dropped context",
                AsyncWaitTimeout);
            barrierReleaseSource.TrySetResult(true);

            var workThreadId = await TestAwaiter.WaitAsync(
                executionTask.AsUniTask(),
                "mutation request resumed by editor update",
                AsyncWaitTimeout);
            Assert.That(workThreadId, Is.EqualTo(mainThreadId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenPendingQueueIsFull_RejectsAdditionalWork () => UniTask.ToCoroutine(async () =>
        {
            using var executor = new UnitySynchronizationContextRequestExecutor(
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                maxPendingInvocations: 1);
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
                maxPendingInvocations: 1);
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
                maxPendingInvocations: 1);
            using var cancellationTokenSource = new CancellationTokenSource();
            var canceledTask = executor.ExecuteAsync(
                () => Task.FromResult(1),
                cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            Assert.That(canceledTask.IsCanceled, Is.True);
            Assert.That(executor.IsBusy, Is.False);
            Assert.That(executor.TrySealAdmissionForRetirement(out var admissionSeal), Is.True);
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
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations);
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
        public IEnumerator ExecuteAsync_WhenCanceledReadOnlyWorkRemainsActive_DoesNotQuarantineLane () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            using var cancellationTokenSource = new CancellationTokenSource();
            var workStarted = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseWork = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var executionTask = executor.ExecuteAsync(async () =>
            {
                workStarted.TrySetResult(null);
                await releaseWork.Task;
                return 1;
            }, cancellationTokenSource.Token);

            await TestAwaiter.WaitAsync(workStarted.Task.AsUniTask(), "Read-only work start", AsyncWaitTimeout);
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await executionTask.AsUniTask();
            }, "Canceled read-only request", AsyncWaitTimeout);
            Assert.That(executor.IsQuarantined, Is.False);
            Assert.That(executor.HasUnfinishedWork, Is.True);

            var nextTask = executor.ExecuteAsync(() => Task.FromResult(2));
            await Task.Yield();
            Assert.That(nextTask.IsCompleted, Is.False, "The next request must remain serialized behind unfinished read work.");

            releaseWork.TrySetResult(null);
            Assert.That(
                await TestAwaiter.WaitAsync(nextTask.AsUniTask(), "Read-only successor", AsyncWaitTimeout),
                Is.EqualTo(2));
            await TestAwaiter.WaitAsync(
                executor.WaitForRetirementAsync().AsUniTask(),
                "Read-only lane retirement",
                AsyncWaitTimeout);
            Assert.That(executor.HasUnfinishedWork, Is.False);
            Assert.That(executor.IsBusy, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenStartedMutationDoesNotQuiesce_QuarantinesUntilWorkTerminates () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            using var cancellationTokenSource = new CancellationTokenSource();
            var mutationStarted = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseMutation = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var executionTask = executor.ExecuteAsync(async () =>
            {
                var mutationActivity = executor.BeginMutation();
                mutationStarted.TrySetResult(null);
                await releaseMutation.Task;
                mutationActivity.Complete();
                return 1;
            }, cancellationTokenSource.Token);

            await TestAwaiter.WaitAsync(mutationStarted.Task.AsUniTask(), "Mutation start", AsyncWaitTimeout);
            cancellationTokenSource.Cancel();
            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await executionTask.AsUniTask();
            }, "Canceled non-cooperative mutation", AsyncWaitTimeout);

            Assert.That(executor.IsQuarantined, Is.True);
            Assert.That(executor.IsBusy, Is.True);
            Assert.That(executor.HasUnfinishedWork, Is.True);
            Assert.That(executor.TrySealAdmissionForRetirement(out var admissionSeal), Is.True);
            var retirementTask = executor.WaitForRetirementAsync();
            Assert.That(retirementTask.IsCompleted, Is.False);
            await AsyncExceptionCapture.CaptureAsync<UnityMutationLaneQuarantinedException>(async () =>
            {
                await executor.ExecuteAsync(() => Task.FromResult(3)).AsUniTask();
            }, "Future mutation after quarantine", AsyncWaitTimeout);

            releaseMutation.TrySetResult(null);
            await TestAwaiter.WaitAsync(retirementTask.AsUniTask(), "Quarantined lane retirement", AsyncWaitTimeout);
            Assert.That(executor.HasUnfinishedWork, Is.False);
            Assert.That(executor.IsQuarantined, Is.True, "Retired generations must continue rejecting admission.");
            admissionSeal.Dispose();
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenActiveMutationQuiescesDuringGrace_DoesNotQuarantine () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            using var cancellationTokenSource = new CancellationTokenSource();
            var mutationStarted = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationObserved = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationRegistration = cancellationTokenSource.Token.Register(
                static state => ((TaskCompletionSource<object>)state).TrySetResult(null),
                cancellationObserved);
            var executionTask = executor.ExecuteAsync(async () =>
            {
                var mutationActivity = executor.BeginMutation();
                mutationStarted.TrySetResult(null);
                await cancellationObserved.Task;
                mutationActivity.Complete();
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                return 1;
            }, cancellationTokenSource.Token);

            await TestAwaiter.WaitAsync(mutationStarted.Task.AsUniTask(), "Cooperative mutation start", AsyncWaitTimeout);
            cancellationTokenSource.Cancel();
            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await executionTask.AsUniTask();
            }, "Cooperatively canceled mutation", AsyncWaitTimeout);
            await TestAwaiter.WaitAsync(
                executor.WaitForRetirementAsync().AsUniTask(),
                "Cooperative mutation retirement",
                AsyncWaitTimeout);
            Assert.That(executor.IsQuarantined, Is.False);
            Assert.That(executor.IsBusy, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenMutationWorkStopsWithoutSafetyConfirmation_QuarantinesBeforePublishingCancellation () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            using var cancellationTokenSource = new CancellationTokenSource();
            var mutationStarted = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationObserved = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var mutationWorkStopped = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            IUnityMutationActivity mutationActivity = null;
            using var cancellationRegistration = cancellationTokenSource.Token.Register(
                static state => ((TaskCompletionSource<object>)state).TrySetResult(null),
                cancellationObserved);
            var executionTask = executor.ExecuteAsync(async () =>
            {
                mutationActivity = executor.BeginMutation();
                mutationStarted.TrySetResult(null);
                await cancellationObserved.Task;
                mutationWorkStopped.TrySetResult(null);
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                return 1;
            }, cancellationTokenSource.Token);

            await TestAwaiter.WaitAsync(mutationStarted.Task.AsUniTask(), "Mutation start", AsyncWaitTimeout);
            cancellationTokenSource.Cancel();
            await TestAwaiter.WaitAsync(mutationWorkStopped.Task.AsUniTask(), "Mutation cancellation observation", AsyncWaitTimeout);
            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await executionTask.AsUniTask();
            }, "Canceled mutation after generation quarantine", AsyncWaitTimeout);

            Assert.That(
                executor.IsQuarantined,
                Is.True,
                "Outward cancellation requires quarantine when explicit mutation safety remains unfinished.");
            Assert.That(executor.HasUnfinishedWork, Is.True);

            mutationActivity.Complete();
            await TestAwaiter.WaitAsync(
                executor.WaitForRetirementAsync().AsUniTask(),
                "Canceled mutation retirement after safety confirmation",
                AsyncWaitTimeout);
            Assert.That(executor.HasUnfinishedWork, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenMutationHasNotStarted_RejectsQuarantineRequest () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            var unfinishedWork = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            var exception = await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(async () =>
            {
                await executor.ExecuteAsync(() =>
                {
                    executor.Quarantine("A read-only request attempted to quarantine the lane.", unfinishedWork.Task);
                    return Task.FromResult(true);
                }).AsUniTask();
            }, "Invalid read-only quarantine", AsyncWaitTimeout);

            Assert.That(
                exception.Message,
                Does.Contain("started Unity mutation"));
            Assert.That(executor.IsQuarantined, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenMutationSafetyCompletedBeforeQuarantine_DoesNotQuarantineLane () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();

            var result = await executor.ExecuteAsync(() =>
            {
                var mutationActivity = executor.BeginMutation();
                mutationActivity.Complete();
                executor.Quarantine(
                    "The cancellation grace elapsed concurrently with mutation safety confirmation.",
                    Task.CompletedTask);
                return Task.FromResult(1);
            });

            Assert.That(result, Is.EqualTo(1));
            Assert.That(executor.IsQuarantined, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ExecuteAsync_WhenRequestReturnsWithUnfinishedMutation_QuarantinesUntilMutationCompletes () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();
            IUnityMutationActivity mutationActivity = null;
            var executionTask = executor.ExecuteAsync(() =>
            {
                mutationActivity = executor.BeginMutation();
                return Task.FromResult(1);
            });

            await TestAwaiter.WaitAsync(
                UniTask.WaitUntil(() => executor.IsQuarantined),
                "Automatic quarantine for an unfinished mutation",
                AsyncWaitTimeout);

            Assert.That(executionTask.IsCompleted, Is.True);
            Assert.That(executionTask.IsCanceled, Is.False);
            Assert.That(executionTask.IsFaulted, Is.False);
            Assert.That(executor.HasUnfinishedWork, Is.True);
            var retirementTask = executor.WaitForRetirementAsync();
            Assert.That(retirementTask.IsCompleted, Is.False);
            await AsyncExceptionCapture.CaptureAsync<UnityMutationLaneQuarantinedException>(async () =>
            {
                await executor.ExecuteAsync(() => Task.FromResult(2)).AsUniTask();
            }, "Request after automatic mutation quarantine", AsyncWaitTimeout);

            mutationActivity.Complete();
            await TestAwaiter.WaitAsync(
                retirementTask.AsUniTask(),
                "Automatic quarantine retirement",
                AsyncWaitTimeout);
            Assert.That(executor.HasUnfinishedWork, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TrySealAdmissionForRetirement_WhenIdle_BlocksNewWorkUntilSealIsReleased () => UniTask.ToCoroutine(async () =>
        {
            using var executor = CreateMutationExecutor();

            Assert.That(executor.TrySealAdmissionForRetirement(out var admissionSeal), Is.True);
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
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations);
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
