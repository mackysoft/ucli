using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Infrastructure.Storage;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Runtime.Versioning;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests;
using MackySoft.Ucli.Tests.Helpers;

public sealed class DaemonSessionStoreTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenSessionGenerationLockIsOwned_DoesNotPublishAnotherSession ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "write-generation-lock-owned");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fingerprint-write-generation-lock-owned",
            sessionToken: "successor-token");
        var lockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(
            scope.FullPath,
            session.ProjectFingerprint);
        using var sessionLock = FileExclusiveLock.Acquire(
            lockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        Assert.False(File.Exists(UcliStoragePathResolver.ResolveSessionPath(
            scope.FullPath,
            session.ProjectFingerprint)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Delete_WhenSessionGenerationLockIsOwned_DoesNotDeleteCurrentSession ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "delete-generation-lock-owned");
        const string projectFingerprint = "fingerprint-delete-generation-lock-owned";
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(sessionPath, "current-session", CancellationToken.None);
        var lockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(scope.FullPath, projectFingerprint);
        using var sessionLock = FileExclusiveLock.Acquire(
            lockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());

        var deleteResult = await store.DeleteAsync(scope.FullPath, projectFingerprint, CancellationToken.None);

        Assert.False(deleteResult.IsSuccess);
        Assert.Equal("current-session", await File.ReadAllTextAsync(sessionPath, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteReadDelete_RoundTripsSessionJson ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "roundtrip");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(projectFingerprint: "fingerprint-roundtrip", sessionToken: "token-1");
        var gitIgnorePath = Path.Combine(
            scope.FullPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.Null(writeResult.Error);
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
        Assert.Equal(UcliContractConstants.LocalDirectoryIgnoreEntry + Environment.NewLine, File.ReadAllText(gitIgnorePath));

        var readResult = await store.ReadAsync(scope.FullPath, session.ProjectFingerprint, CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.True(readResult.Exists);
        var loadedSession = Assert.IsType<DaemonSession>(readResult.Session);
        Assert.Equal(session.SchemaVersion, loadedSession.SchemaVersion);
        Assert.Equal(session.SessionToken, loadedSession.SessionToken);
        Assert.Equal(session.ProjectFingerprint, loadedSession.ProjectFingerprint);
        Assert.Equal(session.EditorMode, loadedSession.EditorMode);
        Assert.Equal(session.OwnerKind, loadedSession.OwnerKind);
        Assert.Equal(session.CanShutdownProcess, loadedSession.CanShutdownProcess);
        Assert.Equal(session.EndpointTransportKind, loadedSession.EndpointTransportKind);
        Assert.Equal(session.EndpointAddress, loadedSession.EndpointAddress);
        Assert.Equal(session.ProcessId, loadedSession.ProcessId);
        Assert.Equal(session.ProcessStartedAtUtc, loadedSession.ProcessStartedAtUtc);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, session.ProjectFingerprint);
        Assert.Equal(
            DaemonSessionArtifactIdentity.Create(await File.ReadAllTextAsync(sessionPath, CancellationToken.None)),
            readResult.ArtifactIdentity);

        var deleteResult = await store.DeleteAsync(scope.FullPath, session.ProjectFingerprint, CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        var readAfterDeleteResult = await store.ReadAsync(scope.FullPath, session.ProjectFingerprint, CancellationToken.None);
        Assert.True(readAfterDeleteResult.IsSuccess);
        Assert.False(readAfterDeleteResult.Exists);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenGitIgnoreAlreadyExists_DoesNotOverwriteExistingContents ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "existing-gitignore");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(projectFingerprint: "fingerprint-existing-gitignore", sessionToken: "token-1");
        var relativeGitIgnorePath = Path.Combine(
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);
        var gitIgnorePath = scope.WriteFile(relativeGitIgnorePath, "legacy/" + Environment.NewLine);

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

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
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(projectFingerprint: "fingerprint-owner-only", sessionToken: "token-1");

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(scope.FullPath);
        var fingerprintDirectoryPath = UcliStoragePathResolver.ResolveFingerprintDirectory(scope.FullPath, session.ProjectFingerprint);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, session.ProjectFingerprint);

        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(localDirectoryPath);
        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(fingerprintDirectoryPath);
        PosixAccessBoundaryAssert.FileIsOwnerOnly(sessionPath);
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
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(projectFingerprint: "fingerprint-current-user-only", sessionToken: "token-1");

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(scope.FullPath);
        var fingerprintDirectoryPath = UcliStoragePathResolver.ResolveFingerprintDirectory(scope.FullPath, session.ProjectFingerprint);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, session.ProjectFingerprint);

        WindowsAccessBoundaryAssert.DirectoryIsCurrentUserOnly(localDirectoryPath);
        WindowsAccessBoundaryAssert.DirectoryIsCurrentUserOnly(fingerprintDirectoryPath);
        WindowsAccessBoundaryAssert.FileIsCurrentUserOnly(sessionPath);
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
            UcliStoragePathNames.FingerprintsDirectoryName);
        Directory.CreateDirectory(Path.GetDirectoryName(blockedPath)!);
        await File.WriteAllTextAsync(blockedPath, "blocked", CancellationToken.None);

        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(projectFingerprint: "fingerprint-blocked", sessionToken: "token-1");

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

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
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, "fingerprint-malformed");
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(sessionPath, "{", CancellationToken.None);

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-malformed", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        Assert.Equal(DaemonSessionArtifactIdentity.Create("{"), readResult.ArtifactIdentity);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenTransportKindIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "invalid-transport");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fingerprint-invalid-transport",
            sessionToken: "token-1") with
        {
            EndpointTransportKind = "unsupported-transport",
        };

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("endpointTransportKind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenEditorModeIsUnsupported_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "invalid-editor-mode");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fingerprint-invalid-editor-mode",
            sessionToken: "token-1") with
        {
            EditorMode = "unsupported",
        };

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("editorMode", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenOwnerKindIsNotCli_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "invalid-owner-kind");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fingerprint-invalid-owner-kind",
            sessionToken: "token-1") with
        {
            OwnerKind = "gui",
        };

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("ownerKind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenSessionFingerprintDoesNotMatchRequestedFingerprint_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "fingerprint-mismatch");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var requestedFingerprint = "fingerprint-requested";
        var mismatchedSession = DaemonSessionTestFactory.Create(projectFingerprint: "fingerprint-other", sessionToken: "token-1");

        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, requestedFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        var serializer = new DaemonSessionJsonSerializer();
        await File.WriteAllTextAsync(
            sessionPath,
            serializer.Serialize(mismatchedSession) + Environment.NewLine,
            CancellationToken.None);

        var readResult = await store.ReadAsync(scope.FullPath, requestedFingerprint, CancellationToken.None);

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
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var requestedFingerprint = "fingerprint-read-invalid-editor-mode";
        var session = DaemonSessionTestFactory.Create(projectFingerprint: requestedFingerprint, sessionToken: "token-1") with
        {
            EditorMode = "unsupported",
        };

        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, requestedFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        var serializer = new DaemonSessionJsonSerializer();
        await File.WriteAllTextAsync(
            sessionPath,
            serializer.Serialize(session) + Environment.NewLine,
            CancellationToken.None);

        var readResult = await store.ReadAsync(scope.FullPath, requestedFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("editorMode", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenOwnerKindIsUserAndCanShutdownProcessIsTrue_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "user-owner-can-shutdown-true");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fingerprint-user-owner-can-shutdown-true",
            sessionToken: "token-1") with
        {
            EditorMode = "gui",
            OwnerKind = "user",
            CanShutdownProcess = true,
        };

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("canShutdownProcess=false", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenGuiUserSessionCannotShutdownProcess_Succeeds ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "gui-user-owner");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fingerprint-gui-user-owner",
            sessionToken: "token-1") with
        {
            EditorMode = "gui",
            OwnerKind = "user",
            CanShutdownProcess = false,
        };

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
    }

}
