using System;
using System.Collections;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class NamedPipeUnityIpcTransportListenerLifetimeTests
    {
        private const int MaximumActiveConnections = 32;

        private const int HandoffStressIterationCount = 16;

        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        private static readonly TimeSpan ConnectionDrainTimeout = TimeSpan.FromSeconds(1);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenAnotherListenerOwnsSamePipe_FailsAtBoundedOwnershipDeadline () => UniTask.ToCoroutine(async () =>
        {
            var address = CreateAddress();
            var owner = CreateListener();
            var contender = CreateListener();
            var ownerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var contenderStarted = false;
            var ownerRunTask = owner.RunAsync(
                address,
                new NoOpConnectionHandler(),
                () => ownerStarted.TrySetResult(true),
                _ => { },
                CancellationToken.None);

            try
            {
                await TestAwaiter.WaitAsync(
                    ownerStarted.Task,
                    "Named pipe endpoint owner start",
                    SignalWaitTimeout);

                var contenderRunTask = contender.RunAsync(
                    address,
                    new NoOpConnectionHandler(),
                    () => contenderStarted = true,
                    _ => { },
                    CancellationToken.None);
                TimeoutException ownershipException = null;
                try
                {
                    await TestAwaiter.WaitAsync(
                        contenderRunTask,
                        "Contended named pipe listener outcome",
                        SignalWaitTimeout);
                }
                catch (TimeoutException exception)
                {
                    ownershipException = exception;
                }

                Assert.That(ownershipException, Is.Not.Null);
                Assert.That(contenderStarted, Is.False);
            }
            finally
            {
                contender.Release();
                owner.Release();
                await WaitForListenerStopAsync(ownerRunTask, "Named pipe endpoint owner cleanup");
                DeleteOwnershipLock(address);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenReservedGenerationIsReleasedBeforeBackgroundEntry_DoesNotStartAndAllowsSuccessor () => UniTask.ToCoroutine(async () =>
        {
            var address = CreateAddress();
            var listener = CreateListener();
            using var releasedCancellationTokenSource = new CancellationTokenSource();
            using var successorCancellationTokenSource = new CancellationTokenSource();
            var releasedGenerationStarted = false;
            var successorStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                listener.ReserveRun(releasedCancellationTokenSource.Token);
                listener.Release();

                await TestAwaiter.WaitAsync(
                    listener.RunAsync(
                        address,
                        new NoOpConnectionHandler(),
                        () => releasedGenerationStarted = true,
                        _ => { },
                        releasedCancellationTokenSource.Token),
                    "Released pre-entry named pipe generation",
                    SignalWaitTimeout);

                listener.ReserveRun(successorCancellationTokenSource.Token);
                var successorRunTask = listener.RunAsync(
                    address,
                    new NoOpConnectionHandler(),
                    () => successorStarted.TrySetResult(true),
                    _ => { },
                    successorCancellationTokenSource.Token);
                await TestAwaiter.WaitAsync(
                    successorStarted.Task,
                    "Successor after released pre-entry named pipe generation",
                    SignalWaitTimeout);

                Assert.That(releasedGenerationStarted, Is.False);

                listener.Release();
                await WaitForListenerStopAsync(
                    successorRunTask,
                    "Successor after pre-entry release cleanup");
            }
            finally
            {
                listener.Release();
                DeleteOwnershipLock(address);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenReleasedGenerationDrainsAfterSameListenerRestarts_DoesNotCloseSuccessor () => UniTask.ToCoroutine(async () =>
        {
            await AssertReleasedGenerationDoesNotCloseSuccessorAsync(useSeparateListenerInstance: false);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenReleasedGenerationDrainsAfterSeparateListenerRestarts_DoesNotCloseSuccessor () => UniTask.ToCoroutine(async () =>
        {
            await AssertReleasedGenerationDoesNotCloseSuccessorAsync(useSeparateListenerInstance: true);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Run_WhenCancellationAndSameListenerRestartRace_RepeatedlyHandsOffOwnership () => UniTask.ToCoroutine(async () =>
        {
            var address = CreateAddress();
            var listener = CreateListener();
            try
            {
                for (var iteration = 0; iteration < HandoffStressIterationCount; iteration++)
                {
                    using var releasedCancellationTokenSource = new CancellationTokenSource();
                    var releasedStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var releasedRunTask = listener.RunAsync(
                        address,
                        new NoOpConnectionHandler(),
                        () => releasedStarted.TrySetResult(true),
                        _ => { },
                        releasedCancellationTokenSource.Token);
                    await TestAwaiter.WaitAsync(
                        releasedStarted.Task,
                        $"Named pipe handoff source start {iteration}",
                        SignalWaitTimeout);

                    releasedCancellationTokenSource.Cancel();
                    releasedCancellationTokenSource.Dispose();

                    using var successorCancellationTokenSource = new CancellationTokenSource();
                    var successorStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var successorRunTask = listener.RunAsync(
                        address,
                        new NoOpConnectionHandler(),
                        () => successorStarted.TrySetResult(true),
                        _ => { },
                        successorCancellationTokenSource.Token);
                    var successorOutcome = await TestAwaiter.WaitAsync(
                        Task.WhenAny(successorStarted.Task, successorRunTask),
                        $"Named pipe handoff successor outcome {iteration}",
                        SignalWaitTimeout);
                    if (ReferenceEquals(successorOutcome, successorRunTask))
                    {
                        await successorRunTask;
                        Assert.Fail($"Named pipe handoff successor {iteration} exited before startup.");
                    }

                    await WaitForListenerStopAsync(
                        releasedRunTask,
                        $"Named pipe handoff source cleanup {iteration}");
                    listener.Release();
                    await WaitForListenerStopAsync(
                        successorRunTask,
                        $"Named pipe handoff successor cleanup {iteration}");
                }
            }
            finally
            {
                listener.Release();
                DeleteOwnershipLock(address);
            }
        });

        private static async Task AssertReleasedGenerationDoesNotCloseSuccessorAsync (
            bool useSeparateListenerInstance)
        {
            var address = CreateAddress();
            var firstListener = CreateListener();
            var successorListener = useSeparateListenerInstance
                ? CreateListener()
                : firstListener;
            var firstConnectionHandler = new ReleasableConnectionHandler();
            var successorConnectionHandler = new CountingConnectionHandler();
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var successorStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstRunTask = firstListener.RunAsync(
                address,
                firstConnectionHandler,
                () => firstStarted.TrySetResult(true),
                _ => { },
                CancellationToken.None);
            Task successorRunTask = null;
            NamedPipeClientStream firstClient = null;

            try
            {
                await TestAwaiter.WaitAsync(
                    firstStarted.Task,
                    "First named pipe listener start",
                    SignalWaitTimeout);
                firstClient = Connect(address);
                await TestAwaiter.WaitAsync(
                    firstConnectionHandler.ConnectionObserved,
                    "First named pipe connection handling",
                    SignalWaitTimeout);

                firstListener.Release();
                successorRunTask = successorListener.RunAsync(
                    address,
                    successorConnectionHandler,
                    () => successorStarted.TrySetResult(true),
                    _ => { },
                    CancellationToken.None);
                await TestAwaiter.WaitAsync(
                    successorStarted.Task,
                    "Successor named pipe listener start",
                    SignalWaitTimeout);

                Assert.That(firstRunTask.IsCompleted, Is.False);

                using (Connect(address))
                {
                    await TestAwaiter.WaitAsync(
                        successorConnectionHandler.FirstConnectionObserved,
                        "Successor first named pipe connection",
                        SignalWaitTimeout);
                }

                firstClient.Dispose();
                firstClient = null;
                firstConnectionHandler.Complete();
                await WaitForReleasedListenerStopAsync(
                    firstRunTask,
                    "Released named pipe generation drain");

                using (Connect(address))
                {
                    await TestAwaiter.WaitAsync(
                        successorConnectionHandler.SecondConnectionObserved,
                        "Successor second named pipe connection",
                        SignalWaitTimeout);
                }

                Assert.That(successorConnectionHandler.CallCount, Is.EqualTo(2));
            }
            finally
            {
                firstClient?.Dispose();
                firstConnectionHandler.Complete();
                firstListener.Release();
                if (!ReferenceEquals(successorListener, firstListener))
                {
                    successorListener.Release();
                }

                await WaitForReleasedListenerStopAsync(
                    firstRunTask,
                    "First named pipe listener final cleanup");
                if (successorRunTask != null)
                {
                    await WaitForListenerStopAsync(
                        successorRunTask,
                        "Successor named pipe listener final cleanup");
                }

                DeleteOwnershipLock(address);
            }
        }

        private static NamedPipeUnityIpcTransportListener CreateListener ()
        {
            return new NamedPipeUnityIpcTransportListener(
                NoOpDaemonLogger.Instance,
                MaximumActiveConnections,
                ConnectionDrainTimeout);
        }

        private static string CreateAddress ()
        {
            return "ucli-np-" + Guid.NewGuid().ToString("N");
        }

        private static NamedPipeClientStream Connect (string address)
        {
            var client = new NamedPipeClientStream(
                ".",
                address,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            try
            {
                client.Connect((int)SignalWaitTimeout.TotalMilliseconds);
                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        private static async Task WaitForListenerStopAsync (
            Task listenerTask,
            string description)
        {
            try
            {
                await TestAwaiter.WaitAsync(listenerTask, description, SignalWaitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static async Task WaitForReleasedListenerStopAsync (
            Task listenerTask,
            string description)
        {
            try
            {
                await WaitForListenerStopAsync(listenerTask, description);
            }
            catch (TimeoutException exception)
            {
                Assert.That(exception.Message, Does.Contain("connection drain"));
            }
        }

        private static void DeleteOwnershipLock (string address)
        {
            var lockPath = NamedPipeUnityIpcTransportListener.ResolveEndpointOwnershipLockPath(address);
            if (File.Exists(lockPath))
            {
                File.Delete(lockPath);
            }
        }

        private sealed class ReleasableConnectionHandler : IUnityIpcConnectionHandler
        {
            private readonly TaskCompletionSource<bool> connectionObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> completion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task ConnectionObserved => connectionObserved.Task;

            public void Complete ()
            {
                completion.TrySetResult(true);
            }

            public async Task<UnityIpcConnectionHandleResult> HandleAsync (
                Stream stream,
                CancellationToken cancellationToken = default)
            {
                connectionObserved.TrySetResult(true);
                await completion.Task;
                return default;
            }
        }

        private sealed class CountingConnectionHandler : IUnityIpcConnectionHandler
        {
            private readonly TaskCompletionSource<bool> firstConnectionObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<bool> secondConnectionObserved =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public int CallCount { get; private set; }

            public Task FirstConnectionObserved => firstConnectionObserved.Task;

            public Task SecondConnectionObserved => secondConnectionObserved.Task;

            public Task<UnityIpcConnectionHandleResult> HandleAsync (
                Stream stream,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                if (CallCount == 1)
                {
                    firstConnectionObserved.TrySetResult(true);
                }
                else if (CallCount == 2)
                {
                    secondConnectionObserved.TrySetResult(true);
                }

                return Task.FromResult(default(UnityIpcConnectionHandleResult));
            }
        }

        private sealed class NoOpConnectionHandler : IUnityIpcConnectionHandler
        {
            public Task<UnityIpcConnectionHandleResult> HandleAsync (
                Stream stream,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(default(UnityIpcConnectionHandleResult));
            }
        }
    }
}
