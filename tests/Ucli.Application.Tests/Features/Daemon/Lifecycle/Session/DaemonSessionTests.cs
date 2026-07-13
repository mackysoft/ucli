using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonSessionTests
{
    private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly ProjectFingerprint Fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenProjectFingerprintIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new DaemonSession(
            IpcSessionTokenTestFactory.Create("null-project-fingerprint"),
            null!,
            new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            DaemonEditorMode.Batchmode,
            DaemonSessionOwnerKind.Cli,
            canShutdownProcess: true,
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-endpoint"),
            processId: 1234,
            processStartedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            ownerProcessId: 5678));

        Assert.Equal("projectFingerprint", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenProcessIdentityIsIncomplete_ThrowsArgumentException ()
    {
        var token = IpcSessionTokenTestFactory.Create("incomplete-process-identity");

        var exception = Assert.Throws<ArgumentException>(() => new DaemonSession(
            token,
            Fingerprint,
            new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            DaemonEditorMode.Batchmode,
            DaemonSessionOwnerKind.Cli,
            canShutdownProcess: true,
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-endpoint"),
            processId: 1234,
            processStartedAtUtc: null,
            ownerProcessId: 5678));

        Assert.Equal("processStartedAtUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenUserOwnedSessionCanShutdownProcess_ThrowsArgumentException ()
    {
        var token = IpcSessionTokenTestFactory.Create("unsafe-user-owned-session");

        var exception = Assert.Throws<ArgumentException>(() => new DaemonSession(
            token,
            Fingerprint,
            new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            DaemonEditorMode.Gui,
            DaemonSessionOwnerKind.User,
            canShutdownProcess: true,
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-endpoint"),
            processId: null,
            processStartedAtUtc: null,
            ownerProcessId: 5678));

        Assert.Equal("ownerKind", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenEditorInstanceIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => DaemonSessionTestFactory.Create(
            editorInstanceId: Guid.Empty));

        Assert.Equal("editorInstanceId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenUserOwnedSessionHasNoEditorInstanceId_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new DaemonSession(
            IpcSessionTokenTestFactory.Create("user-owned-without-editor-instance-id"),
            Fingerprint,
            new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            DaemonEditorMode.Gui,
            DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-endpoint"),
            processId: 1234,
            processStartedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            ownerProcessId: 5678,
            editorInstanceId: null));

        Assert.Equal("editorInstanceId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ValueEquality_WhenEveryFieldHasTheSameValue_ReturnsTrue ()
    {
        var expected = CreateComparableSession();
        var actual = CreateComparableSession();

        Assert.NotSame(expected, actual);
        Assert.Equal(expected, actual);
        Assert.True(expected == actual);
        Assert.Equal(expected.GetHashCode(), actual.GetHashCode());
    }

    [Theory]
    [InlineData(SessionDifference.SessionToken)]
    [InlineData(SessionDifference.ProjectFingerprint)]
    [InlineData(SessionDifference.IssuedAtUtc)]
    [InlineData(SessionDifference.EditorMode)]
    [InlineData(SessionDifference.OwnerKind)]
    [InlineData(SessionDifference.CanShutdownProcess)]
    [InlineData(SessionDifference.Endpoint)]
    [InlineData(SessionDifference.ProcessId)]
    [InlineData(SessionDifference.ProcessStartedAtUtc)]
    [InlineData(SessionDifference.OwnerProcessId)]
    [InlineData(SessionDifference.EditorInstanceId)]
    [Trait("Size", "Small")]
    public void ValueEquality_WhenOneFieldDiffers_ReturnsFalse (SessionDifference difference)
    {
        var sessions = difference switch
        {
            SessionDifference.SessionToken => (CreateComparableSession(), CreateComparableSession(sessionToken: "other-session-token")),
            SessionDifference.ProjectFingerprint => (CreateComparableSession(), CreateComparableSession(
                projectFingerprint: ProjectFingerprintTestFactory.Create("other-project-fingerprint"))),
            SessionDifference.IssuedAtUtc => (CreateComparableSession(), CreateComparableSession(
                issuedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 2, TimeSpan.Zero))),
            SessionDifference.EditorMode => (
                CreateComparableSession(editorMode: "batchmode", ownerKind: "cli", canShutdownProcess: true),
                CreateComparableSession(editorMode: "gui", ownerKind: "cli", canShutdownProcess: true)),
            SessionDifference.OwnerKind => (
                CreateComparableSession(ownerKind: "user"),
                CreateComparableSession(ownerKind: "cli")),
            SessionDifference.CanShutdownProcess => (
                CreateComparableSession(ownerKind: "cli", canShutdownProcess: false),
                CreateComparableSession(ownerKind: "cli", canShutdownProcess: true)),
            SessionDifference.Endpoint => (CreateComparableSession(), CreateComparableSession(endpointAddress: "/tmp/other-ucli.sock")),
            SessionDifference.ProcessId => (CreateComparableSession(), CreateComparableSession(processId: 4321)),
            SessionDifference.ProcessStartedAtUtc => (CreateComparableSession(), CreateComparableSession(
                processStartedAtUtc: new DateTimeOffset(2026, 7, 12, 23, 59, 58, TimeSpan.Zero))),
            SessionDifference.OwnerProcessId => (CreateComparableSession(), CreateComparableSession(ownerProcessId: 8765)),
            SessionDifference.EditorInstanceId => (CreateComparableSession(), CreateComparableSession(editorInstanceId: Guid.Parse("22222222-2222-2222-2222-222222222222"))),
            _ => throw new ArgumentOutOfRangeException(nameof(difference), difference, null),
        };

        Assert.NotEqual(sessions.Item1, sessions.Item2);
        Assert.True(sessions.Item1 != sessions.Item2);
    }

    private static DaemonSession CreateComparableSession (
        string sessionToken = "same-session-token",
        ProjectFingerprint? projectFingerprint = null,
        DateTimeOffset? issuedAtUtc = null,
        string editorMode = "gui",
        string ownerKind = "user",
        bool canShutdownProcess = false,
        string endpointAddress = "/tmp/ucli.sock",
        int processId = 1234,
        DateTimeOffset? processStartedAtUtc = null,
        int ownerProcessId = 5678,
        Guid? editorInstanceId = null)
    {
        return DaemonSessionTestFactory.Create(
            processId: processId,
            sessionToken: sessionToken,
            projectFingerprint: projectFingerprint ?? ProjectFingerprintTestFactory.Create("same-project-fingerprint"),
            issuedAtUtc: issuedAtUtc ?? new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            editorMode: editorMode,
            ownerKind: ownerKind,
            canShutdownProcess: canShutdownProcess,
            endpointTransportKind: "unixDomainSocket",
            endpointAddress: endpointAddress,
            processStartedAtUtc: processStartedAtUtc ?? new DateTimeOffset(2026, 7, 12, 23, 59, 59, TimeSpan.Zero),
            ownerProcessId: ownerProcessId,
            editorInstanceId: editorInstanceId ?? EditorInstanceId);
    }

    public enum SessionDifference
    {
        SessionToken,
        ProjectFingerprint,
        IssuedAtUtc,
        EditorMode,
        OwnerKind,
        CanShutdownProcess,
        Endpoint,
        ProcessId,
        ProcessStartedAtUtc,
        OwnerProcessId,
        EditorInstanceId,
    }
}
