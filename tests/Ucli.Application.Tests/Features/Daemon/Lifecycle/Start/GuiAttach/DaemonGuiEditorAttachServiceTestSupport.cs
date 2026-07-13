using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

internal static class DaemonGuiEditorAttachServiceTestSupport
{
    public static readonly DateTimeOffset ProbeProcessStartedAtUtc = new(2026, 5, 9, 0, 0, 0, TimeSpan.Zero);

    public static readonly ResolvedUnityProjectContext UnityProject = ProjectContextTestFactory.CreateRepositoryFixtureUnityProject(
        projectFingerprint: "fingerprint");

    public static UnityEditorInstanceMarker CreateMarker ()
    {
        return new UnityEditorInstanceMarker(
            MarkerPath: "/repo/UnityProject/Library/EditorInstance.json",
            ProcessId: 1234,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 1, 0, TimeSpan.Zero),
            AppPath: "/Applications/Unity.app",
            AppContentsPath: "/Applications/Unity.app/Contents");
    }

    public static DaemonSession CreateGuiSession ()
    {
        return DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            projectFingerprint: "fingerprint",
            issuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 2, 0, TimeSpan.Zero),
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock",
            processId: 1234,
            ownerProcessId: null);
    }

    public static IpcUnityEditorObservation CreateReadyLifecycleObservation ()
    {
        return IpcUnityEditorObservationTestFactory.Create(
            IpcEditorLifecycleState.Ready,
            DaemonEditorMode.Gui);
    }
}
