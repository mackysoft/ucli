using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonSessionContractMapperTests
{
    private const string SessionToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenEditorModeContainsOuterWhitespace_ReturnsInvalidArgumentWithoutNormalizing ()
    {
        var contract = CreateContract() with
        {
            EditorMode = " batchmode ",
        };

        var isValid = DaemonSessionContractMapper.TryCreate(
            contract,
            "fingerprint",
            "/repository/.ucli/local/fingerprints/fingerprint/session.json",
            out var session,
            out var error);

        Assert.False(isValid);
        Assert.Null(session);
        Assert.Contains("editorMode", Assert.IsType<ExecutionError>(error).Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenContractIsValid_ReturnsTypedRuntimeSession ()
    {
        var contract = CreateContract();

        var isValid = DaemonSessionContractMapper.TryCreate(
            contract,
            "fingerprint",
            "/repository/.ucli/local/fingerprints/fingerprint/session.json",
            out var session,
            out var error);

        Assert.True(isValid);
        Assert.Null(error);
        Assert.NotNull(session);
        Assert.Equal(DaemonEditorMode.Batchmode, session.EditorMode);
        Assert.Equal(DaemonSessionOwnerKind.Cli, session.OwnerKind);
        Assert.Equal(IpcTransportKind.NamedPipe, session.Endpoint.TransportKind);
        Assert.Equal("ucli-daemon-endpoint", session.Endpoint.Address);
        Assert.Equal(SessionToken, session.SessionToken.GetEncodedValue());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToContract_WritesCurrentSchemaAndCanonicalBoundaryValues ()
    {
        Assert.True(IpcSessionToken.TryParse(SessionToken, out var token));
        var session = new DaemonSession(
            token,
            "fingerprint",
            new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            DaemonEditorMode.Gui,
            DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli.sock"),
            processId: 1234,
            processStartedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            ownerProcessId: 5678,
            editorInstanceId: "editor-instance-1");

        var contract = DaemonSessionContractMapper.ToContract(session);

        Assert.Equal(DaemonSessionStorageContract.CurrentSchemaVersion, contract.SchemaVersion);
        Assert.Equal(SessionToken, contract.SessionToken);
        Assert.Equal("gui", contract.EditorMode);
        Assert.Equal("user", contract.OwnerKind);
        Assert.Equal("unixDomainSocket", contract.EndpointTransportKind);
        Assert.Equal("/tmp/ucli.sock", contract.EndpointAddress);
        Assert.Equal("editor-instance-1", contract.EditorInstanceId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenSessionTokenIsNotCanonical_ReturnsInvalidArgument ()
    {
        var contract = CreateContract() with
        {
            SessionToken = "not-a-canonical-token",
        };

        var isValid = DaemonSessionContractMapper.TryCreate(
            contract,
            "fingerprint",
            "/repository/.ucli/local/fingerprints/fingerprint/session.json",
            out var session,
            out var error);

        Assert.False(isValid);
        Assert.Null(session);
        Assert.Contains("sessionToken", Assert.IsType<ExecutionError>(error).Message, StringComparison.Ordinal);
    }

    private static DaemonSessionJsonContract CreateContract ()
    {
        return new DaemonSessionJsonContract(
            SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
            SessionToken: SessionToken,
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            OwnerProcessId: 5678);
    }
}
