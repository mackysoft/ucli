using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonSessionTestFactory
{
    public static readonly Guid DefaultEditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static DaemonSession Create (
        int? processId = 1234,
        string sessionToken = "secret-token",
        string projectFingerprint = "fingerprint",
        DateTimeOffset? issuedAtUtc = null,
        string editorMode = "batchmode",
        string ownerKind = "cli",
        bool canShutdownProcess = true,
        string endpointTransportKind = "namedPipe",
        string endpointAddress = "ucli-daemon-endpoint",
        DateTimeOffset? processStartedAtUtc = null,
        int? ownerProcessId = 9876,
        Guid? editorInstanceId = null)
    {
        if (!ContractLiteralCodec.TryParse<DaemonEditorMode>(editorMode, out var parsedEditorMode))
        {
            throw new ArgumentException("Editor mode label must be a contract literal.", nameof(editorMode));
        }

        if (!ContractLiteralCodec.TryParse<DaemonSessionOwnerKind>(ownerKind, out var parsedOwnerKind))
        {
            throw new ArgumentException("Owner kind label must be a contract literal.", nameof(ownerKind));
        }

        if (!ContractLiteralCodec.TryParse<IpcTransportKind>(endpointTransportKind, out var parsedTransportKind))
        {
            throw new ArgumentException("Transport kind label must be a contract literal.", nameof(endpointTransportKind));
        }

        return new DaemonSession(
            IpcSessionTokenTestFactory.Create(sessionToken),
            projectFingerprint,
            issuedAtUtc ?? new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero),
            parsedEditorMode,
            parsedOwnerKind,
            canShutdownProcess,
            new IpcEndpoint(parsedTransportKind, endpointAddress),
            processId,
            processStartedAtUtc ?? (processId is null
                ? null
                : new DateTimeOffset(2026, 03, 05, 0, 0, 1, TimeSpan.Zero)),
            ownerProcessId ?? throw new ArgumentNullException(nameof(ownerProcessId)),
            editorInstanceId);
    }

    public static DaemonSession CreateUserOwned (
        string editorMode,
        string endpointAddress,
        Guid editorInstanceId)
    {
        return Create(
            sessionToken: "session-token",
            projectFingerprint: "project-fingerprint",
            editorMode: editorMode,
            ownerKind: "user",
            canShutdownProcess: false,
            endpointAddress: endpointAddress,
            editorInstanceId: editorInstanceId);
    }

    public static DaemonSession CreateEditorInstance (string editorMode = "gui")
    {
        return Create(
            sessionToken: "session-token",
            projectFingerprint: ProjectIdentityInfoTestFactory.ProjectFingerprint,
            editorMode: editorMode,
            ownerKind: "user",
            canShutdownProcess: false,
            endpointTransportKind: "unixDomainSocket",
            endpointAddress: "/tmp/ucli.sock",
            processId: 1234,
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10),
            ownerProcessId: 9876,
            editorInstanceId: DefaultEditorInstanceId);
    }
}
