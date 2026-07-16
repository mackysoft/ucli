using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Execution;

/// <summary> Validates build progress stream payloads before they are exposed by the CLI. </summary>
internal static class BuildProgressPayloadValidator
{
    /// <summary> Validates one progress payload against the event contract. </summary>
    public static void Validate<TPayload> (
        string eventName,
        TPayload payload,
        Guid expectedRunId,
        Sha256Digest expectedProfileDigest)
        where TPayload : notnull
    {
        switch (eventName)
        {
            case BuildRunProgressEventNames.Started:
            case BuildRunProgressEventNames.ReadinessCompleted:
            case BuildRunProgressEventNames.RunnerResolved:
            case BuildRunProgressEventNames.RunnerStarted:
            case BuildRunProgressEventNames.RunnerCompleted:
            case BuildRunProgressEventNames.RunnerResultCompleted:
            case BuildRunProgressEventNames.ArtifactsCompleted:
            case BuildRunProgressEventNames.Completed:
                if (payload is BuildProgressEntry progressEntry)
                {
                    ValidateProgressEntry(eventName, progressEntry, expectedRunId, expectedProfileDigest);
                    return;
                }

                FailPayloadTypeMismatch(eventName, nameof(BuildProgressEntry), payload);
                return;
            case BuildRunProgressEventNames.LogEntry:
                if (payload is BuildLogEntry logEntry)
                {
                    ValidateRunId(eventName, logEntry.RunId, expectedRunId);
                    return;
                }

                FailPayloadTypeMismatch(eventName, nameof(BuildLogEntry), payload);
                return;
            case BuildRunProgressEventNames.Diagnostic:
                if (payload is BuildDiagnosticEntry diagnosticEntry)
                {
                    ValidateRunId(eventName, diagnosticEntry.RunId, expectedRunId);
                    return;
                }

                FailPayloadTypeMismatch(eventName, nameof(BuildDiagnosticEntry), payload);
                return;
            default:
                throw new BuildProgressProtocolException($"Unity build progress event is not supported: {eventName}.");
        }
    }

    private static void ValidateProgressEntry (
        string eventName,
        BuildProgressEntry entry,
        Guid expectedRunId,
        Sha256Digest expectedProfileDigest)
    {
        ValidateRunId(eventName, entry.RunId, expectedRunId);
        ValidateProfileDigest(eventName, entry.ProfileDigest, expectedProfileDigest);
        var expectedPhase = GetExpectedPhase(eventName);
        if (expectedPhase.HasValue && entry.Phase != expectedPhase.Value)
        {
            FailField(eventName, nameof(entry.Phase), $"must be '{expectedPhase}' for this event.");
        }

        if (entry.RunnerStatus == IpcBuildReportResult.Unknown)
        {
            FailField(eventName, nameof(entry.RunnerStatus), "must be a terminal runner result or null.");
        }
    }

    private static void ValidateRunId (
        string eventName,
        Guid actualRunId,
        Guid expectedRunId)
    {
        if (actualRunId != expectedRunId)
        {
            throw new BuildProgressProtocolException(
                $"Unity build progress payload runId mismatch for event '{eventName}'. Expected={expectedRunId}, Actual={actualRunId}.");
        }
    }

    private static void ValidateProfileDigest (
        string eventName,
        Sha256Digest actualProfileDigest,
        Sha256Digest expectedProfileDigest)
    {
        if (actualProfileDigest != expectedProfileDigest)
        {
            throw new BuildProgressProtocolException(
                $"Unity build progress payload profileDigest mismatch for event '{eventName}'. Expected={expectedProfileDigest}, Actual={actualProfileDigest}.");
        }
    }

    private static BuildRunProgressPhase? GetExpectedPhase (string eventName)
    {
        return eventName switch
        {
            BuildRunProgressEventNames.Started => BuildRunProgressPhase.Started,
            BuildRunProgressEventNames.ReadinessCompleted => BuildRunProgressPhase.Readiness,
            BuildRunProgressEventNames.RunnerResolved => BuildRunProgressPhase.RunnerResolution,
            BuildRunProgressEventNames.RunnerStarted => BuildRunProgressPhase.RunnerInvocation,
            BuildRunProgressEventNames.RunnerCompleted => BuildRunProgressPhase.RunnerResult,
            BuildRunProgressEventNames.RunnerResultCompleted => BuildRunProgressPhase.RunnerResult,
            BuildRunProgressEventNames.ArtifactsCompleted => BuildRunProgressPhase.ArtifactAccounting,
            BuildRunProgressEventNames.Completed => BuildRunProgressPhase.Completed,
            _ => null,
        };
    }

    private static void FailPayloadTypeMismatch<TPayload> (
        string eventName,
        string expectedTypeName,
        TPayload payload)
        where TPayload : notnull
    {
        throw new BuildProgressProtocolException(
            $"Unity build progress payload type violates contract for event '{eventName}'. Expected={expectedTypeName}, Actual={payload.GetType().Name}.");
    }

    private static void FailField (
        string eventName,
        string fieldName,
        string reason)
    {
        throw new BuildProgressProtocolException(
            $"Unity build progress payload violates contract for event '{eventName}'. Field '{fieldName}' {reason}");
    }
}
