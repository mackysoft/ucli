using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;

/// <summary> Validates Unity test-run progress payloads before publishing public CLI entries. </summary>
internal static class TestRunProgressPayloadValidator
{
    /// <summary> Validates one decoded progress payload against the closed public stream-entry contract. </summary>
    public static void Validate<TPayload> (
        string eventName,
        TPayload payload,
        Guid expectedRunId)
        where TPayload : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        switch (eventName)
        {
            case TestRunProgressEventNames.RunStarted when payload is TestRunStartedEntry entry:
                RequireExpectedRunId(eventName, entry.RunId, expectedRunId);
                return;
            case TestRunProgressEventNames.CaseStarted when payload is TestCaseStartedEntry entry:
                RequireExpectedRunId(eventName, entry.RunId, expectedRunId);
                return;
            case TestRunProgressEventNames.CaseFinished when payload is TestCaseFinishedEntry entry:
                RequireExpectedRunId(eventName, entry.RunId, expectedRunId);
                return;
            case TestRunProgressEventNames.RunDiagnostic when payload is TestRunDiagnosticEntry entry:
                RequireExpectedRunId(eventName, entry.RunId, expectedRunId);
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
        Guid actualRunId,
        Guid expectedRunId)
    {
        if (actualRunId != expectedRunId)
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

}
