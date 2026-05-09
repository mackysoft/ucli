using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests;

public sealed class UnityProjectLockOwnerProbeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeOwner_WhenDaemonSessionProcessIsAlive_ReturnsActiveOwner ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "active-session");
        var unityProject = CreateContext(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new StubDaemonSessionStore(DaemonSessionReadResult.Success(CreateSession(unityProject, Environment.ProcessId))),
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.NotFound("Library/EditorInstance.json")),
            new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Success([])));

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.ActiveOwner, result.Status);
        Assert.Equal(Environment.ProcessId, result.ProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeOwner_WhenDaemonSessionReadFails_ReturnsAmbiguous ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "session-read-failed");
        var unityProject = CreateContext(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new StubDaemonSessionStore(DaemonSessionReadResult.Failure(
                ExecutionError.InternalError("session read failed"),
                DaemonSessionReadFailureKind.IoFailure)),
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.NotFound("Library/EditorInstance.json")),
            new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Success([])));

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.Ambiguous, result.Status);
        Assert.Contains("session read failed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeOwner_WhenEditorInstanceProcessIsAlive_ReturnsActiveOwner ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "active-editor-instance");
        var unityProject = CreateContext(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new StubDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.Active(
                scope.GetPath("UnityProject/Library/EditorInstance.json"),
                Environment.ProcessId)),
            new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Success([])));

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.ActiveOwner, result.Status);
        Assert.Equal(Environment.ProcessId, result.ProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeOwner_WhenEditorInstanceIsAmbiguous_ReturnsAmbiguousWithoutScanningProcesses ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "ambiguous-editor-instance");
        var unityProject = CreateContext(scope);
        var scanner = new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Success([]));
        var probe = new UnityProjectLockOwnerProbe(
            new StubDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.Ambiguous(
                scope.GetPath("UnityProject/Library/EditorInstance.json"),
                "EditorInstance unreadable")),
            scanner);

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.Ambiguous, result.Status);
        Assert.Contains("EditorInstance unreadable", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, scanner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeOwner_WhenProcessScanFindsMatchingProject_ReturnsActiveOwner ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "matching-process-scan");
        var unityProject = CreateContext(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new StubDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.NotFound("Library/EditorInstance.json")),
            new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Success([
                new UnityProjectProcessMatch(12345, unityProject.UnityProjectRoot),
            ])));

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.ActiveOwner, result.Status);
        Assert.Equal(12345, result.ProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeOwner_WhenProcessScanFails_ReturnsAmbiguous ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "scan-failed");
        var unityProject = CreateContext(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new StubDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.NotFound("Library/EditorInstance.json")),
            new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Failure("ps denied")));

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.Ambiguous, result.Status);
        Assert.Contains("ps denied", result.Message, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateContext (TestDirectoryScope scope)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: scope.CreateDirectory("UnityProject"),
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static string CreateLockFilePath (TestDirectoryScope scope)
    {
        return scope.GetPath("UnityProject/Temp/UnityLockfile");
    }

    private static DaemonSession CreateSession (
        ResolvedUnityProjectContext unityProject,
        int? processId)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: unityProject.ProjectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            EndpointTransportKind: IpcTransportKindValues.UnixDomainSocket,
            EndpointAddress: "/tmp/ucli.sock",
            ProcessId: processId,
            OwnerProcessId: null);
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        private readonly DaemonSessionReadResult readResult;

        public StubDaemonSessionStore (DaemonSessionReadResult readResult)
        {
            this.readResult = readResult;
        }

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(readResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubUnityEditorInstanceProbe : IUnityEditorInstanceProbe
    {
        private readonly UnityEditorInstanceProbeResult result;

        public StubUnityEditorInstanceProbe (UnityEditorInstanceProbeResult result)
        {
            this.result = result;
        }

        public ValueTask<UnityEditorInstanceProbeResult> ProbeAsync (
            string unityProjectRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubUnityProjectProcessScanner : IUnityProjectProcessScanner
    {
        private readonly UnityProjectProcessScanResult result;

        public StubUnityProjectProcessScanner (UnityProjectProcessScanResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public ValueTask<UnityProjectProcessScanResult> FindProcessesForProjectAsync (
            string unityProjectRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }
}
