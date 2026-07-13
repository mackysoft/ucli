using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonSessionContractMapperTests
{
    private const string SessionToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly ProjectFingerprint Fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenEditorModeIsMissing_ReturnsInvalidArgument ()
    {
        var contract = CreateContract() with
        {
            EditorMode = null,
        };

        var isValid = DaemonSessionContractMapper.TryCreate(
            contract,
            Fingerprint,
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
        var contract = CreateContract() with
        {
            EditorInstanceId = EditorInstanceId,
        };

        var isValid = DaemonSessionContractMapper.TryCreate(
            contract,
            Fingerprint,
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
        Assert.Equal(EditorInstanceId, session.EditorInstanceId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToContract_WritesCurrentSchemaAndCanonicalBoundaryValues ()
    {
        Assert.True(IpcSessionToken.TryParse(SessionToken, out var token));
        var session = new DaemonSession(
            token,
            Fingerprint,
            new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            DaemonEditorMode.Gui,
            DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli.sock"),
            processId: 1234,
            processStartedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            ownerProcessId: 5678,
            editorInstanceId: EditorInstanceId);

        var contract = DaemonSessionContractMapper.ToContract(session);

        Assert.Equal(DaemonSessionStorageContract.CurrentSchemaVersion, contract.SchemaVersion);
        Assert.Equal(SessionToken, contract.SessionToken);
        Assert.Equal(DaemonEditorMode.Gui, contract.EditorMode);
        Assert.Equal(DaemonSessionOwnerKind.User, contract.OwnerKind);
        Assert.Equal(IpcTransportKind.UnixDomainSocket, contract.EndpointTransportKind);
        Assert.Equal("/tmp/ucli.sock", contract.EndpointAddress);
        Assert.Equal(EditorInstanceId, contract.EditorInstanceId);
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
            Fingerprint,
            "/repository/.ucli/local/fingerprints/fingerprint/session.json",
            out var session,
            out var error);

        Assert.False(isValid);
        Assert.Null(session);
        Assert.Contains("sessionToken", Assert.IsType<ExecutionError>(error).Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenEditorInstanceIdIsEmpty_ReturnsInvalidArgument ()
    {
        var contract = CreateContract() with
        {
            EditorInstanceId = Guid.Empty,
        };

        var isValid = DaemonSessionContractMapper.TryCreate(
            contract,
            Fingerprint,
            "/repository/.ucli/local/fingerprints/fingerprint/session.json",
            out var session,
            out var error);

        Assert.False(isValid);
        Assert.Null(session);
        Assert.Contains("editorInstanceId", Assert.IsType<ExecutionError>(error).Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenUserOwnedSessionHasNoEditorInstanceId_ReturnsInvalidArgument ()
    {
        var contract = CreateContract() with
        {
            EditorMode = DaemonEditorMode.Gui,
            OwnerKind = DaemonSessionOwnerKind.User,
            CanShutdownProcess = false,
            EditorInstanceId = null,
        };

        var isValid = DaemonSessionContractMapper.TryCreate(
            contract,
            Fingerprint,
            "/repository/.ucli/local/fingerprints/fingerprint/session.json",
            out var session,
            out var error);

        Assert.False(isValid);
        Assert.Null(session);
        Assert.Contains("editorInstanceId", Assert.IsType<ExecutionError>(error).Message, StringComparison.Ordinal);
    }

    private static DaemonSessionJsonContract CreateContract ()
    {
        return new DaemonSessionJsonContract(
            SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
            SessionToken: SessionToken,
            ProjectFingerprint: Fingerprint,
            IssuedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            EditorMode: DaemonEditorMode.Batchmode,
            OwnerKind: DaemonSessionOwnerKind.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: IpcTransportKind.NamedPipe,
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            OwnerProcessId: 5678);
    }
}
