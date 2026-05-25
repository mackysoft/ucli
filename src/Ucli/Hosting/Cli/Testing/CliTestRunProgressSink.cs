using System.Globalization;
using MackySoft.Ucli.Application.Features.Testing.Run.Progress;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

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

        entryWriter.WriteTextEntry(CreateTextLine(eventName, payload));
        return ValueTask.CompletedTask;
    }

    private static string CreateTextLine (
        string eventName,
        object payload)
    {
        return (eventName, payload) switch
        {
            (TestRunProgressEventNames.RunStarted, TestRunStartedEntry entry) => string.Concat(
                "test run started",
                FormatProperty(entry.RunId, " runId="),
                FormatProperty(entry.TestPlatform, " platform="),
                FormatProperty(entry.TestFilter, " filter=")),
            (TestRunProgressEventNames.CaseStarted, TestCaseStartedEntry entry) => string.Concat(
                "test case started",
                FormatProperty(entry.TestName, " name="),
                FormatProperty(entry.TestId, " id="),
                FormatProperty(entry.AssemblyName, " assembly=")),
            (TestRunProgressEventNames.CaseFinished, TestCaseFinishedEntry entry) => string.Concat(
                "test case finished",
                FormatProperty(entry.TestName, " name="),
                FormatProperty(entry.Result, " result="),
                FormatProperty(entry.DurationMilliseconds, " durationMs=")),
            (TestRunProgressEventNames.RunDiagnostic, TestRunDiagnosticEntry entry) => string.Concat(
                "test run diagnostic",
                FormatProperty(entry.Severity, " severity="),
                FormatProperty(entry.Code, " code="),
                FormatProperty(entry.Message, " message=")),
            _ => string.Concat(eventName, " ", payload),
        };
    }

    private static string FormatProperty (
        string? value,
        string prefix)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(prefix, value);
    }

    private static string FormatProperty (
        long value,
        string prefix)
    {
        return string.Concat(prefix, value.ToString(CultureInfo.InvariantCulture));
    }
}
