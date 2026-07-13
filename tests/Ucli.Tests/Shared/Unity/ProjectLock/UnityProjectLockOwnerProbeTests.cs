using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Unity;

namespace MackySoft.Ucli.Tests;

public sealed class UnityProjectLockOwnerProbeTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ProbeOwner_WhenDaemonSessionProcessIsAlive_ReturnsActiveOwner ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "active-session");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(DaemonSessionTestFactory.Create(
                    sessionToken: "session-token",
                    projectFingerprint: unityProject.ProjectFingerprint,
                    endpointTransportKind: "unixDomainSocket",
                    endpointAddress: "/tmp/ucli.sock",
                    processId: Environment.ProcessId,
                    ownerProcessId: Environment.ProcessId)),
            },
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.NotFound()),
            new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Success([])));

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.ActiveOwner, result.Status);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ProbeOwner_WhenDaemonSessionReadFails_ReturnsAmbiguous ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "session-read-failed");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Failure(
                    ExecutionError.InternalError("session read failed"),
                    DaemonSessionReadFailureKind.IoFailure,
                    artifactIdentity: null),
            },
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.NotFound()),
            new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Success([])));

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.Ambiguous, result.Status);
        Assert.Contains("session read failed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ProbeOwner_WhenEditorInstanceProcessIsAlive_ReturnsActiveOwner ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "active-editor-instance");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new RecordingDaemonSessionStore(),
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.Active()),
            new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Success([])));

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.ActiveOwner, result.Status);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ProbeOwner_WhenEditorInstanceIsAmbiguous_ReturnsAmbiguousWithoutScanningProcesses ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "ambiguous-editor-instance");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new RecordingDaemonSessionStore(),
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.Ambiguous("EditorInstance unreadable")),
            new UnexpectedUnityProjectProcessScanner());

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.Ambiguous, result.Status);
        Assert.Contains("EditorInstance unreadable", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ProbeOwner_WhenProcessScanFindsMatchingProject_ReturnsActiveOwner ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "matching-process-scan");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new RecordingDaemonSessionStore(),
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.NotFound()),
            new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Success([
                new UnityProjectProcessMatch(12345),
            ])));

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.ActiveOwner, result.Status);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ProbeOwner_WhenProcessScanFails_ReturnsAmbiguous ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-owner-probe", "scan-failed");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
        var probe = new UnityProjectLockOwnerProbe(
            new RecordingDaemonSessionStore(),
            new StubUnityEditorInstanceProbe(UnityEditorInstanceProbeResult.NotFound()),
            new StubUnityProjectProcessScanner(UnityProjectProcessScanResult.Failure("ps denied")));

        var result = await probe.ProbeOwnerAsync(unityProject, CreateLockFilePath(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockOwnerProbeStatus.Ambiguous, result.Status);
        Assert.Contains("ps denied", result.Message, StringComparison.Ordinal);
    }

    private static string CreateLockFilePath (TestDirectoryScope scope)
    {
        return scope.GetPath("UnityProject/Temp/UnityLockfile");
    }

}
