using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Infrastructure.Storage;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Runtime.Versioning;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Tests;
using MackySoft.Ucli.Tests.Helpers;

public sealed class DaemonSessionStoreTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenSessionGenerationLockIsOwned_DoesNotPublishAnotherSession ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "write-generation-lock-owned");
        var store = new DaemonSessionStore();
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-write-generation-lock-owned"),
            sessionToken: "successor-token");
        var lockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(
            AbsolutePath.Parse(scope.FullPath),
            session.ProjectFingerprint);
        using var sessionLock = FileExclusiveLock.Acquire(
            lockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        var writeResult = await store.WriteAsync(AbsolutePath.Parse(scope.FullPath), session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        Assert.False(File.Exists(UcliStoragePathResolver.ResolveSessionPath(
            AbsolutePath.Parse(scope.FullPath),
            session.ProjectFingerprint).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Delete_WhenSessionGenerationLockIsOwned_DoesNotDeleteCurrentSession ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "delete-generation-lock-owned");
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-delete-generation-lock-owned");
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(AbsolutePath.Parse(scope.FullPath), projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath.Value)!);
        await File.WriteAllTextAsync(sessionPath.Value, "current-session", CancellationToken.None);
        var lockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(AbsolutePath.Parse(scope.FullPath), projectFingerprint);
        using var sessionLock = FileExclusiveLock.Acquire(
            lockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var store = new DaemonSessionStore();

        var deleteResult = await store.DeleteAsync(AbsolutePath.Parse(scope.FullPath), projectFingerprint, CancellationToken.None);

        Assert.False(deleteResult.IsSuccess);
        Assert.Equal("current-session", await File.ReadAllTextAsync(sessionPath.Value, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteReadDelete_RoundTripsSessionJson ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "roundtrip");
        var store = new DaemonSessionStore();
        var session = DaemonSessionTestFactory.Create(projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-roundtrip"), sessionToken: "token-1");
        var gitIgnorePath = Path.Combine(
            scope.FullPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);

        var writeResult = await store.WriteAsync(AbsolutePath.Parse(scope.FullPath), session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.Null(writeResult.Error);
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
        Assert.Equal(UcliContractConstants.LocalDirectoryIgnoreEntry + Environment.NewLine, File.ReadAllText(gitIgnorePath));

        var readResult = await store.ReadAsync(AbsolutePath.Parse(scope.FullPath), session.ProjectFingerprint, CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.True(readResult.Exists);
        var loadedSession = Assert.IsType<DaemonSession>(readResult.Session);
        Assert.Equal(session.SessionToken, loadedSession.SessionToken);
        Assert.Equal(session.ProjectFingerprint, loadedSession.ProjectFingerprint);
        Assert.Equal(session.EditorMode, loadedSession.EditorMode);
        Assert.Equal(session.OwnerKind, loadedSession.OwnerKind);
        Assert.Equal(session.CanShutdownProcess, loadedSession.CanShutdownProcess);
        Assert.Equal(session.EndpointContract, loadedSession.EndpointContract);
        Assert.Equal(session.UnixSocketEndpointPath, loadedSession.UnixSocketEndpointPath);
        Assert.Equal(session.ProcessId, loadedSession.ProcessId);
        Assert.Equal(session.ProcessStartedAtUtc, loadedSession.ProcessStartedAtUtc);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(AbsolutePath.Parse(scope.FullPath), session.ProjectFingerprint);
        Assert.Equal(
            DaemonSessionArtifactIdentity.Create(await File.ReadAllBytesAsync(sessionPath.Value, CancellationToken.None)),
            readResult.ArtifactIdentity);
        Assert.Null(readResult.InvalidEvidence);

        var deleteResult = await store.DeleteAsync(AbsolutePath.Parse(scope.FullPath), session.ProjectFingerprint, CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        var readAfterDeleteResult = await store.ReadAsync(AbsolutePath.Parse(scope.FullPath), session.ProjectFingerprint, CancellationToken.None);
        Assert.True(readAfterDeleteResult.IsSuccess);
        Assert.False(readAfterDeleteResult.Exists);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenGitIgnoreAlreadyExists_DoesNotOverwriteExistingContents ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "existing-gitignore");
        var store = new DaemonSessionStore();
        var session = DaemonSessionTestFactory.Create(projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-existing-gitignore"), sessionToken: "token-1");
        var relativeGitIgnorePath = Path.Combine(
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);
        var gitIgnorePath = scope.WriteFile(relativeGitIgnorePath, "legacy/" + Environment.NewLine);

        var writeResult = await store.WriteAsync(AbsolutePath.Parse(scope.FullPath), session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.Equal("legacy/" + Environment.NewLine, File.ReadAllText(gitIgnorePath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task Write_OnUnix_SavesSessionJsonUnderOwnerOnlyBoundary ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "owner-only");
        var store = new DaemonSessionStore();
        var session = DaemonSessionTestFactory.Create(projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-owner-only"), sessionToken: "token-1");

        var writeResult = await store.WriteAsync(AbsolutePath.Parse(scope.FullPath), session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(AbsolutePath.Parse(scope.FullPath));
        var projectDirectoryPath = UcliStoragePathResolver.ResolveProjectDirectory(AbsolutePath.Parse(scope.FullPath), session.ProjectFingerprint);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(AbsolutePath.Parse(scope.FullPath), session.ProjectFingerprint);

        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(localDirectoryPath.Value);
        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(projectDirectoryPath.Value);
        PosixAccessBoundaryAssert.FileIsOwnerOnly(sessionPath.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("windows")]
    public async Task Write_OnWindows_SavesSessionJsonUnderCurrentUserOnlyBoundary ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "current-user-only");
        var store = new DaemonSessionStore();
        var session = DaemonSessionTestFactory.Create(projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-current-user-only"), sessionToken: "token-1");

        var writeResult = await store.WriteAsync(AbsolutePath.Parse(scope.FullPath), session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(AbsolutePath.Parse(scope.FullPath));
        var projectDirectoryPath = UcliStoragePathResolver.ResolveProjectDirectory(AbsolutePath.Parse(scope.FullPath), session.ProjectFingerprint);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(AbsolutePath.Parse(scope.FullPath), session.ProjectFingerprint);

        WindowsAccessBoundaryAssert.DirectoryIsCurrentUserOnly(localDirectoryPath.Value);
        WindowsAccessBoundaryAssert.DirectoryIsCurrentUserOnly(projectDirectoryPath.Value);
        WindowsAccessBoundaryAssert.FileIsCurrentUserOnly(sessionPath.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenSessionDirectoryCannotBeSecured_ReturnsInternalError ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "blocked-session-directory");
        var blockedPath = Path.Combine(
            scope.FullPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.ProjectsDirectoryName);
        Directory.CreateDirectory(Path.GetDirectoryName(blockedPath)!);
        await File.WriteAllTextAsync(blockedPath, "blocked", CancellationToken.None);

        var store = new DaemonSessionStore();
        var session = DaemonSessionTestFactory.Create(projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-blocked"), sessionToken: "token-1");

        var writeResult = await store.WriteAsync(AbsolutePath.Parse(scope.FullPath), session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to write daemon session file", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenSessionJsonIsMalformed_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "malformed-json");
        var store = new DaemonSessionStore();
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(AbsolutePath.Parse(scope.FullPath), ProjectFingerprintTestFactory.Create("fingerprint-malformed"));
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath.Value)!);
        await File.WriteAllTextAsync(sessionPath.Value, "{", CancellationToken.None);

        var readResult = await store.ReadAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprintTestFactory.Create("fingerprint-malformed"), CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        Assert.Equal(
            DaemonSessionArtifactIdentity.Create(System.Text.Encoding.UTF8.GetBytes("{")),
            readResult.ArtifactIdentity);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenUnixSocketAddressExceedsTransportLimit_ReturnsInvalidSession ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "unix-address-too-long");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var store = new DaemonSessionStore();
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-unix-address-too-long");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: projectFingerprint);
        var contract = DaemonSessionContractMapper.ToContract(session) with
        {
            EndpointTransportKind = IpcTransportKind.UnixDomainSocket,
            EndpointAddress = Path.Combine(scope.FullPath, new string('s', 120) + ".sock"),
        };
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath.Value)!);
        await File.WriteAllTextAsync(
            sessionPath.Value,
            DaemonSessionJsonContractSerializer.Serialize(contract),
            CancellationToken.None);

        var readResult = await store.ReadAsync(storageRoot, projectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        Assert.Contains("UTF-8 byte length", Assert.IsType<ExecutionError>(readResult.Error).Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenSessionJsonExceedsStorageLimit_ReturnsIoFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "oversized-json");
        var store = new DaemonSessionStore();
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-oversized-json");
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(AbsolutePath.Parse(scope.FullPath), projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath.Value)!);
        await File.WriteAllBytesAsync(
            sessionPath.Value,
            new byte[DaemonSessionStorageContract.MaximumFileSizeBytes + 1],
            CancellationToken.None);

        var readResult = await store.ReadAsync(AbsolutePath.Parse(scope.FullPath), projectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.IoFailure, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("maximum size", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SessionTimestamp.IssuedAtUtc, "issuedAtUtc")]
    [InlineData(SessionTimestamp.ProcessStartedAtUtc, "processStartedAtUtc")]
    [Trait("Size", "Medium")]
    public async Task Read_WhenSessionTimestampHasNonZeroOffset_ReturnsInvalidArgument (
        SessionTimestamp timestamp,
        string expectedParameterName)
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "non-utc-timestamp");
        var store = new DaemonSessionStore();
        var projectFingerprint = ProjectFingerprintTestFactory.Create($"fingerprint-non-utc-{expectedParameterName}");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: projectFingerprint);
        var contract = DaemonSessionContractMapper.ToContract(session);
        var nonUtcTimestamp = new DateTimeOffset(2026, 7, 13, 9, 0, 0, TimeSpan.FromHours(9));
        var invalidContract = timestamp switch
        {
            SessionTimestamp.IssuedAtUtc => contract with
            {
                IssuedAtUtc = nonUtcTimestamp,
            },
            SessionTimestamp.ProcessStartedAtUtc => contract with
            {
                ProcessStartedAtUtc = nonUtcTimestamp,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, null),
        };
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(AbsolutePath.Parse(scope.FullPath), projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath.Value)!);
        await File.WriteAllTextAsync(
            sessionPath.Value,
            DaemonSessionJsonContractSerializer.Serialize(invalidContract),
            CancellationToken.None);

        var readResult = await store.ReadAsync(AbsolutePath.Parse(scope.FullPath), projectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains(expectedParameterName, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenEditorInstanceIdIsNotGuid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "non-guid-editor-instance-id");
        var store = new DaemonSessionStore();
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-non-guid-editor-instance-id");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: projectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(AbsolutePath.Parse(scope.FullPath), projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath.Value)!);
        var validJson = Serialize(session);
        var serializedEditorInstanceId = DaemonSessionTestFactory.DefaultEditorInstanceId.ToString("D");
        Assert.Contains(serializedEditorInstanceId, validJson, StringComparison.Ordinal);
        var invalidJson = validJson.Replace(serializedEditorInstanceId, "not-a-guid", StringComparison.Ordinal);
        await File.WriteAllTextAsync(sessionPath.Value, invalidJson, CancellationToken.None);

        var readResult = await store.ReadAsync(AbsolutePath.Parse(scope.FullPath), projectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        Assert.Equal(
            DaemonSessionArtifactIdentity.Create(System.Text.Encoding.UTF8.GetBytes(invalidJson)),
            readResult.ArtifactIdentity);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("editorInstanceId", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenSessionFingerprintDoesNotMatchRequestedFingerprint_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "fingerprint-mismatch");
        var store = new DaemonSessionStore();
        var requestedFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-requested");
        var mismatchedSession = DaemonSessionTestFactory.Create(projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-other"), sessionToken: "token-1");

        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(AbsolutePath.Parse(scope.FullPath), requestedFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath.Value)!);
        await File.WriteAllTextAsync(
            sessionPath.Value,
            Serialize(mismatchedSession) + Environment.NewLine,
            CancellationToken.None);

        var readResult = await store.ReadAsync(AbsolutePath.Parse(scope.FullPath), requestedFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("projectFingerprint mismatch", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenEditorModeIsUnsupported_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "read-invalid-editor-mode");
        var store = new DaemonSessionStore();
        var requestedFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-read-invalid-editor-mode");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: requestedFingerprint, sessionToken: "token-1");

        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(AbsolutePath.Parse(scope.FullPath), requestedFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath.Value)!);
        var json = Serialize(session);
        var editorModeProperty = $"\"editorMode\": \"{ContractLiteralCodec.ToValue(session.EditorMode)}\"";
        Assert.Contains(editorModeProperty, json, StringComparison.Ordinal);
        await File.WriteAllTextAsync(
            sessionPath.Value,
            json.Replace(
                editorModeProperty,
                "\"editorMode\": \"unsupported\"",
                StringComparison.Ordinal) + Environment.NewLine,
            CancellationToken.None);

        var readResult = await store.ReadAsync(AbsolutePath.Parse(scope.FullPath), requestedFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("editorMode", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenGuiUserSessionCannotShutdownProcess_Succeeds ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "gui-user-owner");
        var store = new DaemonSessionStore();
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-gui-user-owner"),
            sessionToken: "token-1",
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);

        var writeResult = await store.WriteAsync(AbsolutePath.Parse(scope.FullPath), session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
    }

    private static string Serialize (DaemonSession session)
    {
        return DaemonSessionJsonContractSerializer.Serialize(
            DaemonSessionContractMapper.ToContract(session));
    }

    public enum SessionTimestamp
    {
        IssuedAtUtc,
        ProcessStartedAtUtc,
    }

}
