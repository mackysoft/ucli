namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonStartProgressEntryTestFactory
{
    public static readonly DateTimeOffset SampleStartedAtUtc = new(2026, 03, 12, 1, 2, 0, TimeSpan.Zero);

    public static DaemonStartStartupObservationProgressEntry CreateStartupObservation (
        string? payloadKind = null,
        string projectFingerprint = "fingerprint",
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
            projectFingerprint,
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
