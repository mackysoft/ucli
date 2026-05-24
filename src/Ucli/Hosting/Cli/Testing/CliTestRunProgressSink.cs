using System.Text.Json;
using MackySoft.Ucli.Application.Features.Testing.Run.Progress;
using MackySoft.Ucli.Contracts.Ipc;
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
        var payloadElement = ToJsonElement(payload);
        return eventName switch
        {
            TestRunProgressEventNames.RunStarted => string.Concat(
                "test run started",
                FormatProperty(payloadElement, "runId", " runId="),
                FormatProperty(payloadElement, "testPlatform", " platform="),
                FormatProperty(payloadElement, "testFilter", " filter=")),
            TestRunProgressEventNames.CaseStarted => string.Concat(
                "test case started",
                FormatProperty(payloadElement, "testName", " name="),
                FormatProperty(payloadElement, "testId", " id="),
                FormatProperty(payloadElement, "assemblyName", " assembly=")),
            TestRunProgressEventNames.CaseFinished => string.Concat(
                "test case finished",
                FormatProperty(payloadElement, "testName", " name="),
                FormatProperty(payloadElement, "result", " result="),
                FormatProperty(payloadElement, "durationMilliseconds", " durationMs=")),
            TestRunProgressEventNames.RunDiagnostic => string.Concat(
                "test run diagnostic",
                FormatProperty(payloadElement, "severity", " severity="),
                FormatProperty(payloadElement, "code", " code="),
                FormatProperty(payloadElement, "message", " message=")),
            _ => string.Concat(eventName, " ", payloadElement.GetRawText()),
        };
    }

    private static JsonElement ToJsonElement (object payload)
    {
        return payload is JsonElement payloadElement
            ? payloadElement
            : JsonSerializer.SerializeToElement(payload, IpcJsonSerializerOptions.Default);
    }

    private static string FormatProperty (
        JsonElement payload,
        string propertyName,
        string prefix)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null
            || property.ValueKind == JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        var value = property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.GetRawText();
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(prefix, value);
    }
}
