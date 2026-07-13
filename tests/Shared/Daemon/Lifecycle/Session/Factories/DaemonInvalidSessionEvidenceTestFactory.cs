using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonInvalidSessionEvidenceTestFactory
{
    public static DaemonInvalidSessionEvidence Create (
        string projectFingerprint,
        int? processId = 1234,
        DateTimeOffset? processStartedAtUtc = null,
        int? ownerProcessId = 9876,
        int schemaVersion = DaemonSessionStorageContract.CurrentSchemaVersion,
        string? editorMode = "batchmode",
        string? ownerKind = "cli",
        bool canShutdownProcess = true)
    {
        var contract = new DaemonSessionJsonContract(
            SchemaVersion: schemaVersion,
            SessionToken: "raw-token-is-intentionally-not-projected",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: default,
            EditorMode: editorMode,
            OwnerKind: ownerKind,
            CanShutdownProcess: canShutdownProcess,
            EndpointTransportKind: null,
            EndpointAddress: null,
            ProcessId: processId,
            ProcessStartedAtUtc: processStartedAtUtc ?? (processId is null
                ? null
                : new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero)),
            OwnerProcessId: ownerProcessId);
        return new DaemonInvalidSessionEvidence(contract);
    }
}
