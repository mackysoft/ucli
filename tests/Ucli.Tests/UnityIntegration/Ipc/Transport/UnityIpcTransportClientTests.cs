using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcTransportClientTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_ResolvesEndpointAndForwardsRequest ()
    {
        var transportClient = new RecordingIpcTransportClient();
        var client = new UnityIpcTransportClient(transportClient);
        var request = IpcTransportTestHarness.CreateSingleRequest();
        using var cancellationTokenSource = new CancellationTokenSource();

        var response = await client.SendAsync(
            "storage-root",
            "project-fingerprint",
            request,
            DefaultTimeout,
            cancellationTokenSource.Token);

        Assert.Same(transportClient.SendResponse, response);
        Assert.Equal(
            UcliIpcEndpointResolver.ResolveDaemonEndpoint("storage-root", "project-fingerprint"),
            transportClient.SendEndpoint);
        Assert.Same(request, transportClient.SendRequest);
        Assert.Equal(DefaultTimeout, transportClient.SendTimeout);
        Assert.Equal(cancellationTokenSource.Token, transportClient.SendCancellationToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_ResolvesEndpointAndForwardsProgressCallback ()
    {
        var progressFrame = CreateProgressFrame();
        var transportClient = new RecordingIpcTransportClient
        {
            ProgressFrame = progressFrame,
        };
        var client = new UnityIpcTransportClient(transportClient);
        var request = IpcTransportTestHarness.CreateStreamingRequest();
        var progressFrames = new List<IpcStreamFrame>();
        using var cancellationTokenSource = new CancellationTokenSource();

        var response = await client.SendStreamingAsync(
            "storage-root",
            "project-fingerprint",
            request,
            DefaultTimeout,
            (frame, _) =>
            {
                progressFrames.Add(frame);
                return ValueTask.CompletedTask;
            },
            cancellationTokenSource.Token);

        Assert.Same(transportClient.StreamingResponse, response);
        Assert.Equal(
            UcliIpcEndpointResolver.ResolveDaemonEndpoint("storage-root", "project-fingerprint"),
            transportClient.StreamingEndpoint);
        Assert.Same(request, transportClient.StreamingRequest);
        Assert.Equal(DefaultTimeout, transportClient.StreamingTimeout);
        Assert.Equal(cancellationTokenSource.Token, transportClient.StreamingCancellationToken);
        Assert.Same(progressFrame, Assert.Single(progressFrames));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendAsync_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeExceptionWithoutSendingRequest (int timeoutMilliseconds)
    {
        var transportClient = new RecordingIpcTransportClient();
        var client = new UnityIpcTransportClient(transportClient);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await client.SendAsync(
                    "storage-root",
                    "project-fingerprint",
                    IpcTransportTestHarness.CreateSingleRequest(),
                    TimeSpan.FromMilliseconds(timeoutMilliseconds))
                .AsTask();
        });
        Assert.Null(transportClient.SendEndpoint);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendStreamingAsync_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeExceptionWithoutSendingRequest (int timeoutMilliseconds)
    {
        var transportClient = new RecordingIpcTransportClient();
        var client = new UnityIpcTransportClient(transportClient);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await client.SendStreamingAsync(
                    "storage-root",
                    "project-fingerprint",
                    IpcTransportTestHarness.CreateStreamingRequest(),
                    TimeSpan.FromMilliseconds(timeoutMilliseconds),
                    (_, _) => ValueTask.CompletedTask)
                .AsTask();
        });
        Assert.Null(transportClient.StreamingEndpoint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenCancellationIsRequested_ThrowsOperationCanceledExceptionWithoutSendingRequest ()
    {
        var transportClient = new RecordingIpcTransportClient();
        var client = new UnityIpcTransportClient(transportClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.SendAsync(
                    "storage-root",
                    "project-fingerprint",
                    IpcTransportTestHarness.CreateSingleRequest(),
                    DefaultTimeout,
                    cancellationTokenSource.Token)
                .AsTask();
        });
        Assert.Null(transportClient.SendEndpoint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenCancellationIsRequested_ThrowsOperationCanceledExceptionWithoutSendingRequest ()
    {
        var transportClient = new RecordingIpcTransportClient();
        var client = new UnityIpcTransportClient(transportClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.SendStreamingAsync(
                    "storage-root",
                    "project-fingerprint",
                    IpcTransportTestHarness.CreateStreamingRequest(),
                    DefaultTimeout,
                    (_, _) => ValueTask.CompletedTask,
                    cancellationTokenSource.Token)
                .AsTask();
        });
        Assert.Null(transportClient.StreamingEndpoint);
    }

    private static IpcStreamFrame CreateProgressFrame ()
    {
        return new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            "request-1",
            IpcStreamFrameKinds.Progress,
            "test.progress",
            IpcTransportTestHarness.Json("{}"),
            Response: null);
    }

    private sealed class RecordingIpcTransportClient : IIpcTransportClient
    {
        public IpcResponse SendResponse { get; } = IpcTransportTestHarness.CreateResponse("request-1", """{"sent":true}""");

        public IpcResponse StreamingResponse { get; } = IpcTransportTestHarness.CreateResponse("request-1", """{"streamed":true}""");

        public IpcStreamFrame? ProgressFrame { get; set; }

        public IpcEndpoint? SendEndpoint { get; private set; }

        public IpcRequest? SendRequest { get; private set; }

        public TimeSpan? SendTimeout { get; private set; }

        public CancellationToken SendCancellationToken { get; private set; }

        public IpcEndpoint? StreamingEndpoint { get; private set; }

        public IpcRequest? StreamingRequest { get; private set; }

        public TimeSpan? StreamingTimeout { get; private set; }

        public CancellationToken StreamingCancellationToken { get; private set; }

        public ValueTask<IpcResponse> SendAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            SendEndpoint = endpoint;
            SendRequest = request;
            SendTimeout = timeout;
            SendCancellationToken = cancellationToken;
            return ValueTask.FromResult(SendResponse);
        }

        public async ValueTask<IpcResponse> SendStreamingAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            StreamingEndpoint = endpoint;
            StreamingRequest = request;
            StreamingTimeout = timeout;
            StreamingCancellationToken = cancellationToken;
            if (ProgressFrame is not null)
            {
                await onProgressFrame(ProgressFrame, cancellationToken);
            }

            return StreamingResponse;
        }

        public ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan sendTimeout,
            Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan sendTimeout,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
