using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonSessionTestFactory
{
    public static readonly Guid DefaultSessionGenerationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid DefaultEditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static DaemonSession Create (
        int? processId = 1234,
        string sessionToken = "secret-token",
        ProjectFingerprint? projectFingerprint = null,
        DateTimeOffset? issuedAtUtc = null,
        DaemonEditorMode editorMode = DaemonEditorMode.Batchmode,
        DaemonSessionOwnerKind ownerKind = DaemonSessionOwnerKind.Cli,
        bool canShutdownProcess = true,
        IpcTransportKind endpointTransportKind = IpcTransportKind.NamedPipe,
        string endpointAddress = "ucli-daemon-endpoint",
        DateTimeOffset? processStartedAtUtc = null,
        int? ownerProcessId = 9876,
        Guid? editorInstanceId = null,
        Guid? sessionGenerationId = null)
    {
        return new DaemonSession(
            sessionGenerationId ?? DefaultSessionGenerationId,
            IpcSessionTokenTestFactory.Create(sessionToken),
            projectFingerprint ?? ProjectFingerprintTestFactory.Create("fingerprint"),
            issuedAtUtc ?? new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero),
            editorMode,
            ownerKind,
            canShutdownProcess,
            new IpcEndpoint(endpointTransportKind, endpointAddress),
            processId,
            processStartedAtUtc ?? (processId is null
                ? null
                : new DateTimeOffset(2026, 03, 05, 0, 0, 1, TimeSpan.Zero)),
            ownerProcessId ?? throw new ArgumentNullException(nameof(ownerProcessId)),
            editorInstanceId ?? (ownerKind == DaemonSessionOwnerKind.User
                ? DefaultEditorInstanceId
                : null));
    }

    public static DaemonSession CreateUserOwned (
        DaemonEditorMode editorMode,
        string endpointAddress,
        Guid editorInstanceId)
    {
        return Create(
            sessionToken: "session-token",
            projectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
            editorMode: editorMode,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            endpointAddress: endpointAddress,
            editorInstanceId: editorInstanceId);
    }

    public static DaemonSession CreateEditorInstance ()
    {
        return Create(
            sessionToken: "session-token",
            projectFingerprint: ProjectIdentityInfoTestFactory.ProjectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock",
            processId: 1234,
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10),
            ownerProcessId: 9876,
            editorInstanceId: DefaultEditorInstanceId);
    }
}
