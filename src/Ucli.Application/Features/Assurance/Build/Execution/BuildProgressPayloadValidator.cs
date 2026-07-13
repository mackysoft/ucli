using System.Text;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Execution;

/// <summary> Validates build progress stream payloads before they are exposed by the CLI. </summary>
internal static class BuildProgressPayloadValidator
{
    private static readonly IReadOnlySet<string> ReportRefs = new HashSet<string>(StringComparer.Ordinal)
    {
        BuildReportRefs.Build,
        BuildReportRefs.BuildReport,
        BuildReportRefs.BuildOutputManifest,
        BuildReportRefs.BuildLog,
    };

    /// <summary> Validates one progress payload against the event contract. </summary>
    public static void Validate<TPayload> (
        string eventName,
        TPayload payload,
        Guid expectedRunId,
        string expectedProfileDigest)
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
                    ValidateLogEntry(eventName, logEntry, expectedRunId);
                    return;
                }

                FailPayloadTypeMismatch(eventName, nameof(BuildLogEntry), payload);
                return;
            case BuildRunProgressEventNames.Diagnostic:
                if (payload is BuildDiagnosticEntry diagnosticEntry)
                {
                    ValidateDiagnosticEntry(eventName, diagnosticEntry, expectedRunId);
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
        string expectedProfileDigest)
    {
        ValidateRunId(eventName, entry.RunId, expectedRunId);
        ValidateProfileDigest(eventName, entry.ProfileDigest, expectedProfileDigest);
        if (!ContractLiteralCodec.IsDefined<BuildRunProgressPhase>(entry.Phase))
        {
            FailField(eventName, nameof(entry.Phase), "must be one of the build progress phases.");
        }

        var expectedPhase = GetExpectedPhase(eventName);
        if (expectedPhase != null && !string.Equals(entry.Phase, expectedPhase, StringComparison.Ordinal))
        {
            FailField(eventName, nameof(entry.Phase), $"must be '{expectedPhase}' for this event.");
        }

        if (entry.RunnerKind != null
            && !ContractLiteralCodec.IsDefined<BuildProfileRunnerKind>(entry.RunnerKind))
        {
            FailField(eventName, nameof(entry.RunnerKind), "must be a supported runner kind or null.");
        }

        if (entry.RunnerStatus != null
            && (!ContractLiteralCodec.TryParse<IpcBuildReportResult>(entry.RunnerStatus, out var runnerStatus)
                || runnerStatus == IpcBuildReportResult.Unknown))
        {
            FailField(eventName, nameof(entry.RunnerStatus), "must be a terminal runner result or null.");
        }

        if (entry.Verdict != null
            && !ContractLiteralCodec.IsDefined<BuildVerdict>(entry.Verdict))
        {
            FailField(eventName, nameof(entry.Verdict), "must be a supported build verdict or null.");
        }

        if (entry.ReportRefs == null)
        {
            FailField(eventName, nameof(entry.ReportRefs), "must be present.");
            return;
        }

        var seenRefs = new HashSet<string>(StringComparer.Ordinal);
        var reportRefs = entry.ReportRefs;
        for (var i = 0; i < reportRefs.Length; i++)
        {
            var reportRef = reportRefs[i];
            if (string.IsNullOrWhiteSpace(reportRef)
                || !ReportRefs.Contains(reportRef)
                || !seenRefs.Add(reportRef))
            {
                FailField(eventName, nameof(entry.ReportRefs), "must contain unique stable report refs.");
            }
        }

        if (entry.ErrorCode != null)
        {
            RequireNonEmpty(eventName, nameof(entry.ErrorCode), entry.ErrorCode);
        }
    }

    private static void ValidateLogEntry (
        string eventName,
        BuildLogEntry entry,
        Guid expectedRunId)
    {
        ValidateRunId(eventName, entry.RunId, expectedRunId);
        if (entry.TimestampUtc == default)
        {
            FailField(eventName, nameof(entry.TimestampUtc), "must be present.");
        }

        if (entry.TimestampUtc.Offset != TimeSpan.Zero)
        {
            FailField(eventName, nameof(entry.TimestampUtc), "must use UTC offset.");
        }

        if (!ContractLiteralCodec.IsDefined<BuildLogEntryLevel>(entry.Level))
        {
            FailField(eventName, nameof(entry.Level), "must be a supported log level.");
        }

        RequireNonEmpty(eventName, nameof(entry.Message), entry.Message);
        if (Encoding.UTF8.GetByteCount(entry.Message) > BuildLogEntryLimits.MaxMessageUtf8Bytes)
        {
            FailField(eventName, nameof(entry.Message), "must fit within the progress log message size limit.");
        }

        if (entry.Cursor != null)
        {
            RequireNonEmpty(eventName, nameof(entry.Cursor), entry.Cursor);
        }

        if (!ContractLiteralCodec.IsDefined<BuildLogEntrySource>(entry.Source))
        {
            FailField(eventName, nameof(entry.Source), "must be a supported log source.");
        }
    }

    private static void ValidateDiagnosticEntry (
        string eventName,
        BuildDiagnosticEntry entry,
        Guid expectedRunId)
    {
        ValidateRunId(eventName, entry.RunId, expectedRunId);
        RequireNonEmpty(eventName, nameof(entry.Code), entry.Code);
        if (!IsKnownDiagnosticSeverity(entry.Severity))
        {
            FailField(eventName, nameof(entry.Severity), "must be a supported diagnostic severity.");
        }

        RequireNonEmpty(eventName, nameof(entry.Message), entry.Message);
        if (!ContractLiteralCodec.IsDefined<BuildRunProgressPhase>(entry.Phase))
        {
            FailField(eventName, nameof(entry.Phase), "must be one of the build progress phases.");
        }
    }

    private static bool IsKnownDiagnosticSeverity (string severity)
    {
        return severity is IpcExecuteDiagnosticSeverityNames.Info
            or IpcExecuteDiagnosticSeverityNames.Warning
            or IpcExecuteDiagnosticSeverityNames.Error;
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
        string actualProfileDigest,
        string expectedProfileDigest)
    {
        if (!Sha256LowerHex.IsLowerHexDigest(actualProfileDigest))
        {
            FailField(eventName, nameof(BuildProgressEntry.ProfileDigest), "must be a lowercase SHA-256 digest.");
        }

        if (!string.Equals(actualProfileDigest, expectedProfileDigest, StringComparison.Ordinal))
        {
            throw new BuildProgressProtocolException(
                $"Unity build progress payload profileDigest mismatch for event '{eventName}'. Expected={expectedProfileDigest}, Actual={actualProfileDigest}.");
        }
    }

    private static string? GetExpectedPhase (string eventName)
    {
        return eventName switch
        {
            BuildRunProgressEventNames.Started => ContractLiteralCodec.ToValue(BuildRunProgressPhase.Started),
            BuildRunProgressEventNames.ReadinessCompleted => ContractLiteralCodec.ToValue(BuildRunProgressPhase.Readiness),
            BuildRunProgressEventNames.RunnerResolved => ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerResolution),
            BuildRunProgressEventNames.RunnerStarted => ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerInvocation),
            BuildRunProgressEventNames.RunnerCompleted => ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerResult),
            BuildRunProgressEventNames.RunnerResultCompleted => ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerResult),
            BuildRunProgressEventNames.ArtifactsCompleted => ContractLiteralCodec.ToValue(BuildRunProgressPhase.ArtifactAccounting),
            BuildRunProgressEventNames.Completed => ContractLiteralCodec.ToValue(BuildRunProgressPhase.Completed),
            _ => null,
        };
    }

    private static void RequireNonEmpty (
        string eventName,
        string fieldName,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            FailField(eventName, fieldName, "must not be empty.");
        }
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
