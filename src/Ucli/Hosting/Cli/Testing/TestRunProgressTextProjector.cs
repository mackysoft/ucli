using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Infrastructure.Text;

namespace MackySoft.Ucli.Hosting.Cli.Testing;

/// <summary> Projects test-run progress payloads into human-readable text entries. </summary>
internal sealed class TestRunProgressTextProjector : ICliCommandProgressTextProjector
{
    /// <inheritdoc />
    public bool TryCreateTextEntry<TPayload> (
        string eventName,
        TPayload payload,
        out string text)
        where TPayload : notnull
    {
        switch (eventName, payload)
        {
            case (TestRunProgressEventNames.RunStarted, TestRunStartedEntry):
            case (TestRunProgressEventNames.CaseStarted, TestCaseStartedEntry):
                text = string.Empty;
                return false;
            case (TestRunProgressEventNames.CaseFinished, TestCaseFinishedEntry entry):
                text = CreateCaseFinishedTextLine(entry);
                return true;
            case (TestRunProgressEventNames.RunDiagnostic, TestRunDiagnosticEntry entry):
                text = CreateDiagnosticTextLine(entry);
                return true;
            default:
                text = CliProgressTextFormatter.CreateDelimitedEntry(eventName, " ", payload);
                return true;
        }
    }

    private static string CreateCaseFinishedTextLine (TestCaseFinishedEntry entry)
    {
        var caseResult = FormatCaseResult(entry.Result);
        var durationLength = SpanTextLength.GetInvariantInt64Length(entry.DurationMilliseconds);
        var length = checked(caseResult.Length + 1 + entry.TestName.Length + 2 + durationLength + 4);

        return string.Create(
            length,
            (CaseResult: caseResult, entry.TestName, entry.DurationMilliseconds),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(state.CaseResult);
                writer.Append(' ');
                writer.Append(state.TestName);
                writer.Append(" [");
                writer.AppendInvariant(state.DurationMilliseconds);
                writer.Append(" ms]");
            });
    }

    private static string CreateDiagnosticTextLine (TestRunDiagnosticEntry entry)
    {
        var severity = ContractLiteralCodec.ToValue(entry.Severity);
        var length = checked(severity.Length + 1 + entry.Code.Value.Length + 2 + entry.Message.Length);
        return string.Create(
            length,
            (Severity: severity, Entry: entry),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(state.Severity);
                writer.Append(' ');
                writer.Append(state.Entry.Code.Value);
                writer.Append(": ");
                writer.Append(state.Entry.Message);
            });
    }

    private static string FormatCaseResult (TestCaseResult result)
    {
        return result switch
        {
            TestCaseResult.Pass => "Passed",
            TestCaseResult.Fail => "Failed",
            TestCaseResult.Skipped => "Skipped",
            TestCaseResult.Inconclusive => "Inconclusive",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
        };
    }

}
