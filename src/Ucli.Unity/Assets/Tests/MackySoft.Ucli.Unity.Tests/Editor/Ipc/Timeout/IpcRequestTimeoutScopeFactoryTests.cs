using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class IpcRequestTimeoutScopeFactoryTests
    {
        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CreateLinked_WhenTimeoutElapses_CancelsTokenAndMarksTimeout () => UniTask.ToCoroutine(async () =>
        {
            var timeoutScopeFactory = new IpcRequestTimeoutScopeFactory();
            using var timeoutScope = timeoutScopeFactory.CreateLinked(
                timeoutMilliseconds: 10,
                cancellationToken: CancellationToken.None);

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(60), timeoutScope.Token);
            }, "request timeout scope cancellation", SignalWaitTimeout);

            Assert.That(timeoutScope.IsTimeoutCancellationRequested, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CreateLinked_WhenCallerCancellationIsRequested_CancelsTokenWithoutMarkingTimeout () => UniTask.ToCoroutine(async () =>
        {
            var timeoutScopeFactory = new IpcRequestTimeoutScopeFactory();
            using var cancellationTokenSource = new CancellationTokenSource();
            using var timeoutScope = timeoutScopeFactory.CreateLinked(
                timeoutMilliseconds: 60000,
                cancellationToken: cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(60), timeoutScope.Token);
            }, "request timeout scope caller cancellation", SignalWaitTimeout);

            Assert.That(timeoutScope.IsTimeoutCancellationRequested, Is.False);
        });
    }
}
