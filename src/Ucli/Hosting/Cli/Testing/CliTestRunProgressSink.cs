using MackySoft.Ucli.Application.Features.Testing.Run.Progress;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Infrastructure.Text;

namespace MackySoft.Ucli.Hosting.Cli.Testing;

/// <summary> Projects test-run progress entries to the public CLI entry stream. </summary>
internal sealed class CliTestRunProgressSink : ITestRunProgressSink
{
    private readonly CliStreamEntryFormat format;
    private readonly CliStreamEntryWriter entryWriter;

    /// <summary> Initializes a new instance of the <see cref="CliTestRunProgressSink" /> class. </summary>
    public CliTestRunProgressSink (
        CliStreamEntryFormat format,
        CliStreamEntryWriter entryWriter)
    {
        this.format = format;
        this.entryWriter = entryWriter ?? throw new ArgumentNullException(nameof(entryWriter));
    }

    /// <inheritdoc />
    public ValueTask OnEntryAsync (
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(payload);

        if (format == CliStreamEntryFormat.Json)
        {
            entryWriter.WriteJsonEntry(eventName, payload);
            return ValueTask.CompletedTask;
        }

        if (TryCreateTextLine(eventName, payload, out var textLine))
        {
            entryWriter.WriteTextEntry(textLine);
        }

        return ValueTask.CompletedTask;
    }

    private static bool TryCreateTextLine (
        string eventName,
        object payload,
        out string textLine)
    {
        switch (eventName, payload)
        {
            case (TestRunProgressEventNames.RunStarted, TestRunStartedEntry):
            case (TestRunProgressEventNames.CaseStarted, TestCaseStartedEntry):
                textLine = string.Empty;
                return false;
            case (TestRunProgressEventNames.CaseFinished, TestCaseFinishedEntry entry):
                textLine = CreateCaseFinishedTextLine(entry);
                return true;
            case (TestRunProgressEventNames.RunDiagnostic, TestRunDiagnosticEntry entry):
                textLine = CreateDiagnosticTextLine(entry);
                return true;
            default:
                textLine = string.Concat(eventName, " ", payload);
                return true;
        }
    }

    private static string CreateCaseFinishedTextLine (TestCaseFinishedEntry entry)
    {
        var caseResult = FormatCaseResult(entry.Result);
        var durationLength = GetInt64TextLength(entry.DurationMilliseconds);
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
        var length = checked(entry.Severity.Length + 1 + entry.Code.Length + 2 + entry.Message.Length);
        return string.Create(
            length,
            entry,
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(state.Severity);
                writer.Append(' ');
                writer.Append(state.Code);
                writer.Append(": ");
                writer.Append(state.Message);
            });
    }

    private static string FormatCaseResult (string result)
    {
        return result switch
        {
            "pass" => "Passed",
            "fail" => "Failed",
            "skipped" => "Skipped",
            "inconclusive" => "Inconclusive",
            _ => result,
        };
    }

    private static int GetInt64TextLength (long value)
    {
        if (value == 0)
        {
            return 1;
        }

        var length = value < 0 ? 1 : 0;
        var remaining = value;
        while (remaining != 0)
        {
            length++;
            remaining /= 10;
        }

        return length;
    }
}
