using System;
using System.Threading;
using System.Threading.Tasks;
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

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await server.StartAsync(null);
            });
        }

        [Test]
        [Category("Size.Small")]
        public void StartAsync_WhenAddressIsWhitespace_ThrowsArgumentException ()
        {
            var server = new UnityIpcServer();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, " ");

            Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await server.StartAsync(endpoint);
            });
        }

        [Test]
        [Category("Size.Small")]
        public async Task StartAsync_ThenStopAsync_TransitionsRunningState ()
        {
            var server = new UnityIpcServer();
            var endpoint = new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test");

            await server.StartAsync(endpoint);
            Assert.That(server.IsRunning, Is.True);

            await server.StopAsync();
            Assert.That(server.IsRunning, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void StopAsync_WhenCanceled_ThrowsOperationCanceledException ()
        {
            var server = new UnityIpcServer();
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await server.StopAsync(cancellationTokenSource.Token);
            });
        }
    }
}
