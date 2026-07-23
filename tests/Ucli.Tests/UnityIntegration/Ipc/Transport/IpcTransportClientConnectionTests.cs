using System.Net.Sockets;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientConnectionTests
{
    public static TheoryData<Exception> ExpectedConnectionFailures => new()
    {
        new SocketException((int)SocketError.ConnectionRefused),
        new IOException("Named pipe connection failed."),
    };

    public static TheoryData<Exception> NonTransportConnectionFailures => new()
    {
        new InvalidOperationException("Connector invariant failed."),
        new UnauthorizedAccessException("Endpoint access was denied."),
        new OutOfMemoryException("Synthetic fatal failure."),
    };

    [Theory]
    [MemberData(nameof(ExpectedConnectionFailures))]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenConnectorReportsExpectedIoFailure_ThrowsConnectException (Exception connectorFailure)
    {
        var client = new IpcTransportClient(
            new ThrowingConnector(connectorFailure),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<IpcConnectException>(async () =>
        {
            await client.SendAsync(
                IpcTransportEndpoint.FromNamedPipeAddress("test-transport"),
                IpcTransportTestHarness.CreateSingleRequest(),
                IpcTransportClientTestSupport.DefaultTimeout);
        });

        Assert.Same(connectorFailure, exception.InnerException);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenConnectorReportsTransportTimeout_ThrowsConnectTimeoutException ()
    {
        var connectorFailure = new TimeoutException("Connector timed out.");
        var client = new IpcTransportClient(
            new ThrowingConnector(connectorFailure),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<IpcConnectTimeoutException>(async () =>
        {
            await client.SendAsync(
                IpcTransportEndpoint.FromNamedPipeAddress("test-transport"),
                IpcTransportTestHarness.CreateSingleRequest(),
                IpcTransportClientTestSupport.DefaultTimeout);
        });

        Assert.Same(connectorFailure, exception.InnerException);
    }

    [Theory]
    [MemberData(nameof(NonTransportConnectionFailures))]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenConnectorReportsNonTransportFailure_PreservesException (Exception connectorFailure)
    {
        var client = new IpcTransportClient(
            new ThrowingConnector(connectorFailure),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync(connectorFailure.GetType(), async () =>
        {
            await client.SendAsync(
                IpcTransportEndpoint.FromNamedPipeAddress("test-transport"),
                IpcTransportTestHarness.CreateSingleRequest(),
                IpcTransportClientTestSupport.DefaultTimeout);
        });

        Assert.Same(connectorFailure, exception);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenUnixSocketPathIsMissing_ThrowsConnectException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var endpoint = IpcTransportEndpoint.FromUnixSocketPath(
            new UnixSocketFallbackPath(
                AbsolutePath.Parse(Path.GetTempPath()),
                UnixSocketFallbackPurpose.Supervisor,
                Guid.NewGuid().ToString("N")).SocketPath);
        var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);

        var exception = await Assert.ThrowsAsync<IpcConnectException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                client.SendAsync(endpoint, IpcTransportTestHarness.CreateSingleRequest(), IpcTransportClientTestSupport.DefaultTimeout).AsTask(),
                "Missing Unix socket send result",
                IpcTransportClientTestSupport.WaitTimeout);
        });

        Assert.IsType<SocketException>(exception.InnerException);
        Assert.Contains("before the request was sent", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenNamedPipeServerIsMissing_ThrowsConnectTimeoutException ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var endpoint = IpcTransportEndpoint.FromNamedPipeAddress($"ucli-missing-{Guid.NewGuid():N}");
        var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);

        var exception = await Assert.ThrowsAsync<IpcConnectTimeoutException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                client.SendAsync(endpoint, IpcTransportTestHarness.CreateSingleRequest(), IpcTransportClientTestSupport.DefaultTimeout).AsTask(),
                "Missing named pipe send result",
                IpcTransportClientTestSupport.WaitTimeout);
        });
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var endpoint = IpcTransportEndpoint.FromNamedPipeAddress($"ucli-missing-{Guid.NewGuid():N}");
        var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
        using var cancellationTokenSource = new CancellationTokenSource();

        var sendTask = client.SendAsync(
                endpoint,
                IpcTransportTestHarness.CreateSingleRequest(),
                TimeSpan.FromSeconds(5),
                cancellationTokenSource.Token)
            .AsTask();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                sendTask,
                "Canceled IPC send",
                IpcTransportClientTestSupport.WaitTimeout);
        });
    }

    private sealed class ThrowingConnector : IIpcTransportConnector
    {
        private readonly Exception exception;

        public ThrowingConnector (Exception exception)
        {
            this.exception = exception;
        }

        public ValueTask<Stream> ConnectAsync (
            IpcTransportEndpoint endpoint,
            CancellationToken cancellationToken)
        {
            _ = endpoint;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromException<Stream>(exception);
        }
    }
}
