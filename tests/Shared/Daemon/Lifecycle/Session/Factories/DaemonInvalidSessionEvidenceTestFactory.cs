using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonInvalidSessionEvidenceTestFactory
{
    public static DaemonInvalidSessionEvidence Create (
        ProjectFingerprint projectFingerprint,
        int? processId = 1234,
        DateTimeOffset? processStartedAtUtc = null)
    {
        var contract = new DaemonSessionJsonContract(
            SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
            SessionToken: "raw-token-is-intentionally-not-projected",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: default,
            EditorMode: DaemonEditorMode.Batchmode,
            OwnerKind: DaemonSessionOwnerKind.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: null,
            EndpointAddress: null,
            ProcessId: processId,
            ProcessStartedAtUtc: processStartedAtUtc ?? (processId is null
                ? null
                : new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero)),
            OwnerProcessId: 9876);
        return new DaemonInvalidSessionEvidence(contract);
    }
}
