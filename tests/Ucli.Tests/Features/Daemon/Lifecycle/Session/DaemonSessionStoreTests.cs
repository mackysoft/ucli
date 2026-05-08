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
    [Trait("Size", "Small")]
    public async Task WriteReadDelete_RoundTripsSessionJson ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "roundtrip");
        var store = new DaemonSessionStore();
        var session = CreateSession(projectFingerprint: "fingerprint-roundtrip", sessionToken: "token-1");
        var gitIgnorePath = Path.Combine(
            scope.FullPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.Null(writeResult.Error);
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
        Assert.Equal(UcliContractConstants.LocalDirectoryIgnoreEntry + Environment.NewLine, File.ReadAllText(gitIgnorePath));

        var readResult = await store.Read(scope.FullPath, session.ProjectFingerprint, CancellationToken.None);

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

        var deleteResult = await store.Delete(scope.FullPath, session.ProjectFingerprint, CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        var readAfterDeleteResult = await store.Read(scope.FullPath, session.ProjectFingerprint, CancellationToken.None);
        Assert.True(readAfterDeleteResult.IsSuccess);
        Assert.False(readAfterDeleteResult.Exists);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenGitIgnoreAlreadyExists_DoesNotOverwriteExistingContents ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "existing-gitignore");
        var store = new DaemonSessionStore();
        var session = CreateSession(projectFingerprint: "fingerprint-existing-gitignore", sessionToken: "token-1");
        var relativeGitIgnorePath = Path.Combine(
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);
        var gitIgnorePath = scope.WriteFile(relativeGitIgnorePath, "legacy/" + Environment.NewLine);

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.Equal("legacy/" + Environment.NewLine, File.ReadAllText(gitIgnorePath));
    }

    [Fact]
    [Trait("Size", "Small")]
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
        var session = CreateSession(projectFingerprint: "fingerprint-owner-only", sessionToken: "token-1");

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(scope.FullPath);
        var fingerprintDirectoryPath = UcliStoragePathResolver.ResolveFingerprintDirectory(scope.FullPath, session.ProjectFingerprint);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, session.ProjectFingerprint);

        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(localDirectoryPath);
        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(fingerprintDirectoryPath);
        PosixAccessBoundaryAssert.FileIsOwnerOnly(sessionPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("windows")]
    public async Task Write_OnWindows_SavesSessionJsonUnderCurrentUserOnlyBoundary ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "current-user-only");
        var store = new DaemonSessionStore();
        var session = CreateSession(projectFingerprint: "fingerprint-current-user-only", sessionToken: "token-1");

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(scope.FullPath);
        var fingerprintDirectoryPath = UcliStoragePathResolver.ResolveFingerprintDirectory(scope.FullPath, session.ProjectFingerprint);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, session.ProjectFingerprint);

        WindowsAccessBoundaryAssert.DirectoryIsCurrentUserOnly(localDirectoryPath);
        WindowsAccessBoundaryAssert.DirectoryIsCurrentUserOnly(fingerprintDirectoryPath);
        WindowsAccessBoundaryAssert.FileIsCurrentUserOnly(sessionPath);
    }

    [Fact]
    [Trait("Size", "Small")]
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

        var store = new DaemonSessionStore();
        var session = CreateSession(projectFingerprint: "fingerprint-blocked", sessionToken: "token-1");

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to write daemon session file", error.Message, StringComparison.Ordinal);
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
    public async Task Write_WhenEditorModeIsUnsupported_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "invalid-editor-mode");
        var store = new DaemonSessionStore();
        var session = CreateSession(
            projectFingerprint: "fingerprint-invalid-editor-mode",
            sessionToken: "token-1") with
        {
            EditorMode = "unsupported",
        };

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("editorMode", error.Message, StringComparison.Ordinal);
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
    public async Task Read_WhenEditorModeIsUnsupported_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "read-invalid-editor-mode");
        var store = new DaemonSessionStore();
        var requestedFingerprint = "fingerprint-read-invalid-editor-mode";
        var session = CreateSession(projectFingerprint: requestedFingerprint, sessionToken: "token-1") with
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

        var readResult = await store.Read(scope.FullPath, requestedFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("editorMode", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenOwnerKindIsUserAndCanShutdownProcessIsTrue_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "user-owner-can-shutdown-true");
        var store = new DaemonSessionStore();
        var session = CreateSession(
            projectFingerprint: "fingerprint-user-owner-can-shutdown-true",
            sessionToken: "token-1") with
        {
            EditorMode = DaemonEditorModeValues.Gui,
            OwnerKind = DaemonSessionOwnerKindValues.User,
            CanShutdownProcess = true,
        };

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("canShutdownProcess=false", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenGuiUserSessionCannotShutdownProcess_Succeeds ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "gui-user-owner");
        var store = new DaemonSessionStore();
        var session = CreateSession(
            projectFingerprint: "fingerprint-gui-user-owner",
            sessionToken: "token-1") with
        {
            EditorMode = DaemonEditorModeValues.Gui,
            OwnerKind = DaemonSessionOwnerKindValues.User,
            CanShutdownProcess = false,
        };

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
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
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test",
            ProcessId: 1234,

            OwnerProcessId: 9876);
    }
}
