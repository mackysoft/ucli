using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcTransportClientTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_ResolvesEndpointAndForwardsRequest ()
    {
        var sendResponse = IpcTransportTestHarness.CreateResponse("request-1", """{"sent":true}""");
        var transportClient = new RecordingIpcTransportClient(_ => sendResponse);
        var client = new UnityIpcTransportClient(transportClient);
        var request = IpcTransportTestHarness.CreateSingleRequest();
        using var cancellationTokenSource = new CancellationTokenSource();

        var response = await client.SendAsync(
            "storage-root",
            "project-fingerprint",
            request,
            DefaultTimeout,
            cancellationTokenSource.Token);

        Assert.Same(sendResponse, response);
        UnityIpcTransportClientAssert.SendForwardedToResolvedEndpoint(
            transportClient,
            UcliIpcEndpointResolver.ResolveDaemonEndpoint("storage-root", "project-fingerprint"),
            request,
            DefaultTimeout,
            cancellationTokenSource.Token);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_ResolvesEndpointAndForwardsProgressCallback ()
    {
        var progressFrame = CreateProgressFrame();
        var streamingResponse = IpcTransportTestHarness.CreateResponse("request-1", """{"streamed":true}""");
        var transportClient = new RecordingIpcTransportClient(_ => streamingResponse, _ => progressFrame);
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

        Assert.Same(streamingResponse, response);
        UnityIpcTransportClientAssert.StreamingSendForwardedToResolvedEndpoint(
            transportClient,
            UcliIpcEndpointResolver.ResolveDaemonEndpoint("storage-root", "project-fingerprint"),
            request,
            DefaultTimeout,
            cancellationTokenSource.Token);
        Assert.Same(progressFrame, Assert.Single(progressFrames));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendAsync_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeExceptionWithoutSendingRequest (int timeoutMilliseconds)
    {
        var transportClient = new RecordingIpcTransportClient(_ => IpcTransportTestHarness.CreateResponse("unused", "{}"));
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
        UnityIpcTransportClientAssert.NoEndpointRequestWasSent(transportClient);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendStreamingAsync_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeExceptionWithoutSendingRequest (int timeoutMilliseconds)
    {
        var transportClient = new RecordingIpcTransportClient(_ => IpcTransportTestHarness.CreateResponse("unused", "{}"));
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
        UnityIpcTransportClientAssert.NoEndpointRequestWasSent(transportClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenCancellationIsRequested_ThrowsOperationCanceledExceptionWithoutSendingRequest ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => IpcTransportTestHarness.CreateResponse("unused", "{}"));
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
        UnityIpcTransportClientAssert.NoEndpointRequestWasSent(transportClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenCancellationIsRequested_ThrowsOperationCanceledExceptionWithoutSendingRequest ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => IpcTransportTestHarness.CreateResponse("unused", "{}"));
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
        UnityIpcTransportClientAssert.NoEndpointRequestWasSent(transportClient);
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

}
