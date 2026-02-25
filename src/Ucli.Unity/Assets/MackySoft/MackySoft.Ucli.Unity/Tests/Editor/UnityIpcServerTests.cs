using System;
using System.Threading;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcServerTests
    {

        [Test]
        [Category("Size.Small")]
        public void StartAsync_WhenEndpointIsNull_ThrowsArgumentNullException ()
        {
            var server = new UnityIpcServer();

            Assert.Throws<ArgumentNullException>(() =>
            {
                server.StartAsync(null).GetAwaiter().GetResult();
            });
        }

        [Test]
        [Category("Size.Small")]
        public void StartAsync_WhenAddressIsWhitespace_ThrowsArgumentException ()
        {
            var server = new UnityIpcServer();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, " ");

            Assert.Throws<ArgumentException>(() =>
            {
                server.StartAsync(endpoint).GetAwaiter().GetResult();
            });
        }

        [Test]
        [Category("Size.Small")]
        public void StartAsync_ThenStopAsync_TransitionsRunningState ()
        {
            var server = new UnityIpcServer();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test");

            server.StartAsync(endpoint).GetAwaiter().GetResult();
            Assert.That(server.IsRunning, Is.True);

            server.StopAsync().GetAwaiter().GetResult();
            Assert.That(server.IsRunning, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void StopAsync_WhenCanceled_ThrowsOperationCanceledException ()
        {
            var server = new UnityIpcServer();
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
            {
                server.StopAsync(cancellationTokenSource.Token).GetAwaiter().GetResult();
            });
        }
    }
}
