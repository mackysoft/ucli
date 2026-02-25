using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcServerTests
    {

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartAsync_WhenEndpointIsNull_ThrowsArgumentNullException ()
        {
            var server = new UnityIpcServer();

            return UniTask.ToCoroutine(async () =>
            {
                var exception = await CaptureExceptionAsync<ArgumentNullException>(async () =>
                {
                    await server.StartAsync(null).AsUniTask();
                });

                Assert.That(exception.ParamName, Is.EqualTo("endpoint"));
            });
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartAsync_WhenAddressIsWhitespace_ThrowsArgumentException ()
        {
            var server = new UnityIpcServer();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, " ");

            return UniTask.ToCoroutine(async () =>
            {
                var exception = await CaptureExceptionAsync<ArgumentException>(async () =>
                {
                    await server.StartAsync(endpoint).AsUniTask();
                });

                Assert.That(exception.ParamName, Is.EqualTo("endpoint"));
            });
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartAsync_ThenStopAsync_TransitionsRunningState ()
        {
            var server = new UnityIpcServer();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test");

            return UniTask.ToCoroutine(async () =>
            {
                await server.StartAsync(endpoint).AsUniTask();
                Assert.That(server.IsRunning, Is.True);

                await server.StopAsync().AsUniTask();
                Assert.That(server.IsRunning, Is.False);
            });
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StopAsync_WhenCanceled_ThrowsOperationCanceledException ()
        {
            var server = new UnityIpcServer();
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            return UniTask.ToCoroutine(async () =>
            {
                await CaptureExceptionAsync<OperationCanceledException>(async () =>
                {
                    await server.StopAsync(cancellationTokenSource.Token).AsUniTask();
                });
            });
        }

        private static async UniTask<TException> CaptureExceptionAsync<TException> (Func<UniTask> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"{typeof(TException).Name} was expected.");
            return null;
        }
    }
}
