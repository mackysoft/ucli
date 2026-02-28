using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonPingClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_SendsPingRequestWithProbeContract ()
    {
        var unityIpcClient = new StubUnityIpcClient();
        var pingClient = new IpcDaemonPingClient(unityIpcClient);
        var context = CreateContext();

        await pingClient.Ping(context, CancellationToken.None);

        Assert.Equal(1, unityIpcClient.CallCount);
        Assert.Equal(context.UnityProjectRoot, unityIpcClient.LastProjectRoot);
        Assert.Equal(context.ProjectFingerprint, unityIpcClient.LastProjectFingerprint);
        var request = Assert.IsType<IpcRequest>(unityIpcClient.LastRequest);
        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal(IpcMethodNames.Ping, request.Method);
        Assert.Equal("mode-probe", request.SessionToken);
        Assert.StartsWith("mode-probe-", request.RequestId, StringComparison.Ordinal);
        Assert.Equal("ucli-mode-probe", request.Payload.GetProperty("clientVersion").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenCanceled_ThrowsOperationCanceledException ()
    {
        var unityIpcClient = new StubUnityIpcClient();
        var pingClient = new IpcDaemonPingClient(unityIpcClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await pingClient.Ping(CreateContext(), cancellationTokenSource.Token);
        });
        Assert.Equal(0, unityIpcClient.CallCount);
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: projectRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption,
            ConfigPath: Path.Combine(projectRoot, ".ucli", "config.json"));
    }

    private sealed class StubUnityIpcClient : IUnityIpcClient
    {
        public int CallCount { get; private set; }

        public string? LastProjectRoot { get; private set; }

        public string? LastProjectFingerprint { get; private set; }

        public IpcRequest? LastRequest { get; private set; }

        public ValueTask<IpcResponse> SendAsync (
            string projectRoot,
            string projectFingerprint,
            IpcRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastProjectRoot = projectRoot;
            LastProjectFingerprint = projectFingerprint;
            LastRequest = request;

            return ValueTask.FromResult(new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: request.RequestId,
                Status: "ok",
                Payload: JsonDocument.Parse("{}").RootElement.Clone(),
                Errors: Array.Empty<IpcError>()));
        }
    }
}
