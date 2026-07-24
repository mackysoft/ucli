using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcTransportClientTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);
    private static readonly AbsolutePath StorageRoot = AbsolutePath.Resolve(
        AbsolutePath.Parse(Environment.CurrentDirectory),
        "storage-root");

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_ResolvesEndpointAndForwardsRequest ()
    {
        var request = IpcTransportTestHarness.CreateSingleRequest();
        var sendResponse = IpcTransportTestHarness.CreateResponse(request.RequestId, """{"sent":true}""");
        var transportClient = new RecordingIpcTransportClient(_ => sendResponse);
        var client = new UnityIpcTransportClient(transportClient);
        using var cancellationTokenSource = new CancellationTokenSource();

        var response = await client.SendAsync(
            StorageRoot,
            ProjectFingerprintTestFactory.Create("project-fingerprint"),
            request,
            DefaultTimeout,
            cancellationTokenSource.Token);

        Assert.Same(sendResponse, response);
        UnityIpcTransportClientAssert.SendForwardedToResolvedEndpoint(
            transportClient,
            UcliIpcEndpointResolver.ResolveDaemonEndpoint(StorageRoot, ProjectFingerprintTestFactory.Create("project-fingerprint")).Contract,
            request,
            DefaultTimeout,
            cancellationTokenSource.Token);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_ResolvesEndpointAndForwardsProgressCallback ()
    {
        var request = IpcTransportTestHarness.CreateStreamingRequest();
        var progressFrame = CreateProgressFrame(request.RequestId);
        var streamingResponse = IpcTransportTestHarness.CreateResponse(request.RequestId, """{"streamed":true}""");
        var transportClient = new RecordingIpcTransportClient(_ => streamingResponse, _ => progressFrame);
        var client = new UnityIpcTransportClient(transportClient);
        var progressFrames = new List<IpcStreamFrame>();
        using var cancellationTokenSource = new CancellationTokenSource();

        var response = await client.SendStreamingAsync(
            StorageRoot,
            ProjectFingerprintTestFactory.Create("project-fingerprint"),
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
            UcliIpcEndpointResolver.ResolveDaemonEndpoint(StorageRoot, ProjectFingerprintTestFactory.Create("project-fingerprint")).Contract,
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
        var transportClient = new RecordingIpcTransportClient(_ => IpcTransportTestHarness.CreateResponse(Guid.NewGuid(), "{}"));
        var client = new UnityIpcTransportClient(transportClient);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await client.SendAsync(
                    StorageRoot,
                    ProjectFingerprintTestFactory.Create("project-fingerprint"),
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
        var transportClient = new RecordingIpcTransportClient(_ => IpcTransportTestHarness.CreateResponse(Guid.NewGuid(), "{}"));
        var client = new UnityIpcTransportClient(transportClient);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await client.SendStreamingAsync(
                    StorageRoot,
                    ProjectFingerprintTestFactory.Create("project-fingerprint"),
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
        var transportClient = new RecordingIpcTransportClient(_ => IpcTransportTestHarness.CreateResponse(Guid.NewGuid(), "{}"));
        var client = new UnityIpcTransportClient(transportClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.SendAsync(
                    StorageRoot,
                    ProjectFingerprintTestFactory.Create("project-fingerprint"),
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
        var transportClient = new RecordingIpcTransportClient(_ => IpcTransportTestHarness.CreateResponse(Guid.NewGuid(), "{}"));
        var client = new UnityIpcTransportClient(transportClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.SendStreamingAsync(
                    StorageRoot,
                    ProjectFingerprintTestFactory.Create("project-fingerprint"),
                    IpcTransportTestHarness.CreateStreamingRequest(),
                    DefaultTimeout,
                    (_, _) => ValueTask.CompletedTask,
                    cancellationTokenSource.Token)
                .AsTask();
        });
        UnityIpcTransportClientAssert.NoEndpointRequestWasSent(transportClient);
    }

    private static IpcStreamFrame CreateProgressFrame (Guid requestId)
    {
        return new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            requestId,
            IpcStreamFrameKind.Progress,
            "test.progress",
            IpcTransportTestHarness.Json("{}"),
            response: null);
    }

}
