namespace MackySoft.Ucli.Tests.Helpers.Daemon;

using MackySoft.Ucli.Contracts.Storage;

internal static class DaemonStartProgressEntryTestFactory
{
    private static readonly ProjectFingerprint DefaultProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");

    public static readonly DateTimeOffset SampleStartedAtUtc = new(2026, 03, 12, 1, 2, 0, TimeSpan.Zero);

    public static DaemonStartStartupObservationProgressEntry CreateStartupObservation (
        DaemonStartProgressPayloadKind payloadKind = DaemonStartProgressPayloadKind.StartupObservation,
        ProjectFingerprint? projectFingerprint = null,
        int timeoutMilliseconds = 1234,
        DaemonEditorMode editorMode = DaemonEditorMode.Batchmode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked = DaemonStartupBlockedProcessPolicy.Auto,
        string? launchAttemptId = "attempt-1",
        DaemonSessionOwnerKind ownerKind = DaemonSessionOwnerKind.Cli,
        bool canShutdownProcess = true,
        int? processId = 1234,
        DateTimeOffset? startedAtUtc = null,
        DaemonStartupStatus? startupStatus = null,
        DaemonStartupBlockingReason? startupBlockingReason = null,
        DaemonDiagnosisStartupPhase? startupPhase = null,
        DaemonStartupRetryDisposition? retryDisposition = null,
        string? message = null,
        string? errorCode = null)
    {
        return new DaemonStartStartupObservationProgressEntry(
            payloadKind,
            projectFingerprint ?? DefaultProjectFingerprint,
            timeoutMilliseconds,
            editorMode,
            onStartupBlocked,
            launchAttemptId,
            ownerKind,
            canShutdownProcess,
            processId,
            startedAtUtc,
            startupStatus,
            startupBlockingReason,
            startupPhase,
            retryDisposition,
            message,
            errorCode);
    }
}
