namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonStartProgressEntryTestFactory
{
    private static readonly ProjectFingerprint DefaultProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");

    public static readonly DateTimeOffset SampleStartedAtUtc = new(2026, 03, 12, 1, 2, 0, TimeSpan.Zero);

    public static DaemonStartStartupObservationProgressEntry CreateStartupObservation (
        string? payloadKind = null,
        ProjectFingerprint? projectFingerprint = null,
        int timeoutMilliseconds = 1234,
        string editorMode = "batchmode",
        string onStartupBlocked = "auto",
        string? launchAttemptId = "attempt-1",
        string ownerKind = "cli",
        bool canShutdownProcess = true,
        int? processId = 1234,
        DateTimeOffset? startedAtUtc = null,
        string? startupStatus = null,
        string? startupBlockingReason = null,
        string? startupPhase = null,
        string? retryDisposition = null,
        string? message = null,
        string? errorCode = null)
    {
        return new DaemonStartStartupObservationProgressEntry(
            payloadKind ?? ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
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
