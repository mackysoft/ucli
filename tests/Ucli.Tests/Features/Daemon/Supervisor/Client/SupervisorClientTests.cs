using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenNamedPipeConnectTimesOutAndProcessIsDead_ReturnsUnreachable ()
    {
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new IpcConnectTimeoutException("connect timeout"),
        };
        var client = new SupervisorClient(transportClient);
        var manifest = new SupervisorInstanceManifest(
            ProcessId: int.MaxValue,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));

        var result = await client.ProbeReachabilityAsync(
            manifest,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.Unreachable, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenNamedPipeConnectTimesOutAndProcessIsAlive_ReturnsTimedOut ()
    {
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new IpcConnectTimeoutException("connect timeout"),
        };
        var client = new SupervisorClient(transportClient);
        var manifest = new SupervisorInstanceManifest(
            ProcessId: Environment.ProcessId,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));

        var result = await client.ProbeReachabilityAsync(
            manifest,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.TimedOut, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenFailurePayloadContainsDiagnosis_ReturnsFailureWithDiagnosis ()
    {
        var diagnosis = CreateDiagnosis();
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, timeout, cancellationToken) => ValueTask.FromResult(new IpcResponse(
                ProtocolVersion: request.ProtocolVersion,
                RequestId: request.RequestId,
                Status: IpcProtocol.StatusError,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningFailureResponse(diagnosis)),
                Errors:
                [
                    new IpcError(ExecutionErrorCodes.IpcTimeout, "endpoint registration timed out", null),
                ])),
        };
        var client = new SupervisorClient(transportClient);

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromMilliseconds(100),
            editorMode: DaemonEditorMode.Gui,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        Assert.Equal(diagnosis, result.Diagnosis);
    }

    private static SupervisorInstanceManifest CreateManifest ()
    {
        return new SupervisorInstanceManifest(
            ProcessId: Environment.ProcessId,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
    }

    private static ResolvedUnityProjectContext CreateUnityProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonDiagnosis CreateDiagnosis ()
    {
        return new DaemonDiagnosis(
            Reason: DaemonDiagnosisReasonValues.GuiEndpointNotRegistered,
            Message: "GUI endpoint not registered.",
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 3, 0, TimeSpan.Zero),
            ProcessId: 1234,
            EditorInstancePath: "/repo/UnityProject/Library/EditorInstance.json",
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 2, 0, TimeSpan.Zero));
    }
}
