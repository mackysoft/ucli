using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonSessionTestFactory
{
    public static DaemonSession Create (
        int? processId = 1234,
        string sessionToken = "secret-token",
        string projectFingerprint = "fingerprint",
        DateTimeOffset? issuedAtUtc = null,
        DaemonEditorMode editorMode = DaemonEditorMode.Batchmode,
        DaemonSessionOwnerKind ownerKind = DaemonSessionOwnerKind.Cli,
        bool canShutdownProcess = true,
        IpcTransportKind endpointTransportKind = IpcTransportKind.NamedPipe,
        string endpointAddress = "ucli-daemon-endpoint",
        DateTimeOffset? processStartedAtUtc = null,
        int? ownerProcessId = 9876,
        string? editorInstanceId = null)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: sessionToken,
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: issuedAtUtc ?? new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero),
            EditorMode: editorMode,
            OwnerKind: ownerKind,
            CanShutdownProcess: canShutdownProcess,
            EndpointTransportKind: endpointTransportKind,
            EndpointAddress: endpointAddress,
            ProcessId: processId,
            ProcessStartedAtUtc: processStartedAtUtc ?? (processId is null ? null : DateTimeOffset.UtcNow),
            OwnerProcessId: ownerProcessId)
        {
            EditorInstanceId = editorInstanceId,
        };
    }

    public static DaemonSession CreateUserOwned (
        DaemonEditorMode editorMode,
        string endpointAddress,
        string? editorInstanceId = null)
    {
        return Create(
            sessionToken: "session-token",
            projectFingerprint: "project-fingerprint",
            editorMode: editorMode,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            endpointAddress: endpointAddress,
            editorInstanceId: editorInstanceId);
    }

    public static DaemonSession CreateEditorInstance (DaemonEditorMode editorMode = DaemonEditorMode.Gui)
    {
        return Create(
            sessionToken: "session-token",
            projectFingerprint: ProjectIdentityInfoTestFactory.ProjectFingerprint,
            editorMode: editorMode,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock",
            processId: 1234,
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10),
            ownerProcessId: null,
            editorInstanceId: "editor-instance-1");
    }
}
