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
        public IEnumerator StartAsync_WhenEndpointIsNull_ThrowsArgumentNullException () => UniTask.ToCoroutine(async () =>
        {
            var server = new UnityIpcServer();
            var exception = await AsyncExceptionCapture.CaptureAsync<ArgumentNullException>(async () =>
            {
                await server.StartAsync(null).AsUniTask();
            });

            Assert.That(exception.ParamName, Is.EqualTo("endpoint"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartAsync_WhenAddressIsWhitespace_ThrowsArgumentException () => UniTask.ToCoroutine(async () =>
        {
            var server = new UnityIpcServer();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, " ");
            var exception = await AsyncExceptionCapture.CaptureAsync<ArgumentException>(async () =>
            {
                await server.StartAsync(endpoint).AsUniTask();
            });

            Assert.That(exception.ParamName, Is.EqualTo("endpoint"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartAsync_ThenStopAsync_TransitionsRunningState () => UniTask.ToCoroutine(async () =>
        {
            var server = new UnityIpcServer();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test");
            await server.StartAsync(endpoint).AsUniTask();
            Assert.That(server.IsRunning, Is.True);

            await server.StopAsync().AsUniTask();
            Assert.That(server.IsRunning, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StopAsync_WhenCanceled_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var server = new UnityIpcServer();
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await server.StopAsync(cancellationTokenSource.Token).AsUniTask();
            });
        });
    }

    internal static class AsyncExceptionCapture
    {

        public static async UniTask<TException> CaptureAsync<TException> (Func<UniTask> action)
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
