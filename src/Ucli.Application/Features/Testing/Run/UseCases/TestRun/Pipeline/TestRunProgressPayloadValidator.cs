using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;

/// <summary> Validates Unity test-run progress payloads before publishing public CLI entries. </summary>
internal static class TestRunProgressPayloadValidator
{
    private static readonly HashSet<string> ValidCaseResults = new(StringComparer.Ordinal)
    {
        "pass",
        "fail",
        "skipped",
        "inconclusive",
    };

    private static readonly HashSet<string> ValidDiagnosticSeverities = new(StringComparer.Ordinal)
    {
        "info",
        "warning",
        "error",
    };

    /// <summary> Validates one decoded progress payload against the closed public stream-entry contract. </summary>
    public static void Validate<TPayload> (
        string eventName,
        TPayload payload,
        string expectedRunId)
        where TPayload : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRunId);

        switch (eventName)
        {
            case TestRunProgressEventNames.RunStarted when payload is TestRunStartedEntry entry:
                RequireExpectedRunId(eventName, entry.RunId, expectedRunId);
                ValidateRunStarted(eventName, entry);
                return;
            case TestRunProgressEventNames.CaseStarted when payload is TestCaseStartedEntry entry:
                RequireExpectedRunId(eventName, entry.RunId, expectedRunId);
                ValidateCaseStarted(eventName, entry);
                return;
            case TestRunProgressEventNames.CaseFinished when payload is TestCaseFinishedEntry entry:
                RequireExpectedRunId(eventName, entry.RunId, expectedRunId);
                ValidateCaseFinished(eventName, entry);
                return;
            case TestRunProgressEventNames.RunDiagnostic when payload is TestRunDiagnosticEntry entry:
                RequireExpectedRunId(eventName, entry.RunId, expectedRunId);
                ValidateRunDiagnostic(eventName, entry);
                return;
            case TestRunProgressEventNames.RunStarted:
                FailPayloadTypeMismatch(eventName, nameof(TestRunStartedEntry), payload);
                return;
            case TestRunProgressEventNames.CaseStarted:
                FailPayloadTypeMismatch(eventName, nameof(TestCaseStartedEntry), payload);
                return;
            case TestRunProgressEventNames.CaseFinished:
                FailPayloadTypeMismatch(eventName, nameof(TestCaseFinishedEntry), payload);
                return;
            case TestRunProgressEventNames.RunDiagnostic:
                FailPayloadTypeMismatch(eventName, nameof(TestRunDiagnosticEntry), payload);
                return;
            default:
                throw new TestRunProgressProtocolException(
                    $"Unity test-run progress event is not supported: {eventName}.");
        }
    }

    private static void RequireExpectedRunId (
        string eventName,
        string actualRunId,
        string expectedRunId)
    {
        if (!string.Equals(actualRunId, expectedRunId, StringComparison.Ordinal))
        {
            throw new TestRunProgressProtocolException(
                $"Unity test-run progress payload runId mismatch for event '{eventName}'. Expected={expectedRunId}, Actual={actualRunId}.");
        }
    }

    private static void FailPayloadTypeMismatch<TPayload> (
        string eventName,
        string expectedTypeName,
        TPayload payload)
        where TPayload : notnull
    {
        throw new TestRunProgressProtocolException(
            $"Unity test-run progress payload type violates contract for event '{eventName}'. Expected={expectedTypeName}, Actual={payload.GetType().Name}.");
    }

    private static void ValidateRunStarted (
        string eventName,
        TestRunStartedEntry entry)
    {
        RequireNonEmpty(eventName, "runId", entry.RunId);
        RequireNonEmpty(eventName, "testPlatform", entry.TestPlatform);
        RequireArray(eventName, "assemblyNames", entry.AssemblyNames);
        RequireArray(eventName, "testCategories", entry.TestCategories);
    }

    private static void ValidateCaseStarted (
        string eventName,
        TestCaseStartedEntry entry)
    {
        ValidateCaseIdentity(
            eventName,
            entry.RunId,
            entry.TestId,
            entry.TestName,
            entry.AssemblyName,
            entry.TestPlatform,
            entry.Categories);
    }

    private static void ValidateCaseFinished (
        string eventName,
        TestCaseFinishedEntry entry)
    {
        ValidateCaseIdentity(
            eventName,
            entry.RunId,
            entry.TestId,
            entry.TestName,
            entry.AssemblyName,
            entry.TestPlatform,
            entry.Categories);
        RequireLiteral(eventName, "result", entry.Result, ValidCaseResults);
        if (entry.DurationMilliseconds < 0)
        {
            Fail(eventName, "durationMilliseconds", "must be greater than or equal to zero");
        }
    }

    private static void ValidateRunDiagnostic (
        string eventName,
        TestRunDiagnosticEntry entry)
    {
        RequireNonEmpty(eventName, "runId", entry.RunId);
        RequireNonEmpty(eventName, "code", entry.Code);
        RequireNonEmpty(eventName, "message", entry.Message);
        RequireLiteral(eventName, "severity", entry.Severity, ValidDiagnosticSeverities);
    }

    private static void ValidateCaseIdentity (
        string eventName,
        string runId,
        string testId,
        string testName,
        string? assemblyName,
        string testPlatform,
        IReadOnlyList<string>? categories)
    {
        RequireNonEmpty(eventName, "runId", runId);
        RequireNonEmpty(eventName, "testId", testId);
        RequireNonEmpty(eventName, "testName", testName);
        RequireOptionalNonEmpty(eventName, "assemblyName", assemblyName);
        RequireNonEmpty(eventName, "testPlatform", testPlatform);
        RequireArray(eventName, "categories", categories);
    }

    private static void RequireNonEmpty (
        string eventName,
        string fieldName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Fail(eventName, fieldName, "must not be empty");
        }
    }

    private static void RequireOptionalNonEmpty (
        string eventName,
        string fieldName,
        string? value)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            Fail(eventName, fieldName, "must be null or non-empty");
        }
    }

    private static void RequireArray (
        string eventName,
        string fieldName,
        IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            Fail(eventName, fieldName, "must not be null");
        }

        for (var i = 0; i < values!.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
            {
                Fail(eventName, $"{fieldName}[{i}]", "must not be empty");
            }
        }
    }

    private static void RequireLiteral (
        string eventName,
        string fieldName,
        string? value,
        IReadOnlySet<string> validValues)
    {
        if (string.IsNullOrWhiteSpace(value) || !validValues.Contains(value))
        {
            Fail(eventName, fieldName, $"must be one of: {string.Join(", ", validValues)}");
        }
    }

    private static void Fail (
        string eventName,
        string fieldName,
        string reason)
    {
        throw new TestRunProgressProtocolException(
            $"Unity test-run progress payload violates contract for event '{eventName}'. Field '{fieldName}' {reason}.");
    }
}
