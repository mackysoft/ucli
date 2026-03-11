namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Foundation;

public sealed class DaemonSessionStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteReadDelete_RoundTripsSessionJson ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "roundtrip");
        var store = new DaemonSessionStore();
        var session = CreateSession(projectFingerprint: "fingerprint-roundtrip", sessionToken: "token-1");

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.Null(writeResult.Error);

        var readResult = await store.Read(scope.FullPath, session.ProjectFingerprint, CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.True(readResult.Exists);
        var loadedSession = Assert.IsType<DaemonSession>(readResult.Session);
        Assert.Equal(session.SchemaVersion, loadedSession.SchemaVersion);
        Assert.Equal(session.SessionToken, loadedSession.SessionToken);
        Assert.Equal(session.ProjectFingerprint, loadedSession.ProjectFingerprint);
        Assert.Equal(session.RuntimeKind, loadedSession.RuntimeKind);
        Assert.Equal(session.OwnerKind, loadedSession.OwnerKind);
        Assert.Equal(session.CanShutdownProcess, loadedSession.CanShutdownProcess);
        Assert.Equal(session.EndpointTransportKind, loadedSession.EndpointTransportKind);
        Assert.Equal(session.EndpointAddress, loadedSession.EndpointAddress);
        Assert.Equal(session.ProcessId, loadedSession.ProcessId);

        var deleteResult = await store.Delete(scope.FullPath, session.ProjectFingerprint, CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        var readAfterDeleteResult = await store.Read(scope.FullPath, session.ProjectFingerprint, CancellationToken.None);
        Assert.True(readAfterDeleteResult.IsSuccess);
        Assert.False(readAfterDeleteResult.Exists);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenSessionJsonIsMalformed_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "malformed-json");
        var store = new DaemonSessionStore();
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, "fingerprint-malformed");
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(sessionPath, "{", CancellationToken.None);

        var readResult = await store.Read(scope.FullPath, "fingerprint-malformed", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenTransportKindIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "invalid-transport");
        var store = new DaemonSessionStore();
        var session = CreateSession(
            projectFingerprint: "fingerprint-invalid-transport",
            sessionToken: "token-1") with
        {
            EndpointTransportKind = "unsupported-transport",
        };

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("endpointTransportKind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenRuntimeKindIsNotBatchmode_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "invalid-runtime-kind");
        var store = new DaemonSessionStore();
        var session = CreateSession(
            projectFingerprint: "fingerprint-invalid-runtime-kind",
            sessionToken: "token-1") with
        {
            RuntimeKind = "gui",
        };

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("runtimeKind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenOwnerKindIsNotCli_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "invalid-owner-kind");
        var store = new DaemonSessionStore();
        var session = CreateSession(
            projectFingerprint: "fingerprint-invalid-owner-kind",
            sessionToken: "token-1") with
        {
            OwnerKind = "gui",
        };

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("ownerKind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenSessionFingerprintDoesNotMatchRequestedFingerprint_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "fingerprint-mismatch");
        var store = new DaemonSessionStore();
        var requestedFingerprint = "fingerprint-requested";
        var mismatchedSession = CreateSession(projectFingerprint: "fingerprint-other", sessionToken: "token-1");

        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, requestedFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        var serializer = new DaemonSessionJsonSerializer();
        await File.WriteAllTextAsync(
            sessionPath,
            serializer.Serialize(mismatchedSession) + Environment.NewLine,
            CancellationToken.None);

        var readResult = await store.Read(scope.FullPath, requestedFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("projectFingerprint mismatch", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenRuntimeKindIsNotBatchmode_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "read-invalid-runtime-kind");
        var store = new DaemonSessionStore();
        var requestedFingerprint = "fingerprint-read-invalid-runtime";
        var session = CreateSession(projectFingerprint: requestedFingerprint, sessionToken: "token-1") with
        {
            RuntimeKind = "gui",
        };

        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, requestedFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        var serializer = new DaemonSessionJsonSerializer();
        await File.WriteAllTextAsync(
            sessionPath,
            serializer.Serialize(session) + Environment.NewLine,
            CancellationToken.None);

        var readResult = await store.Read(scope.FullPath, requestedFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("runtimeKind", error.Message, StringComparison.Ordinal);
    }

    private static DaemonSession CreateSession (
        string projectFingerprint,
        string sessionToken)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: sessionToken,
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-test",
            ProcessId: 1234,

            OwnerProcessId: 9876);
    }
}