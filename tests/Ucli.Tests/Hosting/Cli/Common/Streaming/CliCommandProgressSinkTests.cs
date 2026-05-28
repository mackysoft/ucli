using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Common.Streaming;

public sealed class CliCommandProgressSinkTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task OnEntryAsync_WithJsonFormat_WritesEntryEnvelopeWithIncrementingSequence ()
    {
        var standardError = new StringWriter();
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-03-05T10:30:00+00:00"));
        var sink = new CliCommandProgressSink(
            CliStreamEntryFormat.Json,
            new CliStreamEntryWriter("sample.command", standardError, timeProvider),
            new ThrowingTextProjector());

        await sink.OnEntryAsync("sample.started", new { name = "first" }, CancellationToken.None);
        await sink.OnEntryAsync("sample.finished", new { name = "second" }, CancellationToken.None);

        var lines = standardError.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var firstEntry = JsonDocument.Parse(lines[0]);
        using var secondEntry = JsonDocument.Parse(lines[1]);
        AssertEntryEnvelope(firstEntry.RootElement, sequence: 1, eventName: "sample.started");
        AssertEntryEnvelope(secondEntry.RootElement, sequence: 2, eventName: "sample.finished");
        Assert.Equal(
            firstEntry.RootElement.GetProperty("streamId").GetString(),
            secondEntry.RootElement.GetProperty("streamId").GetString());
        Assert.Equal("first", firstEntry.RootElement.GetProperty("payload").GetProperty("name").GetString());
        Assert.Equal("second", secondEntry.RootElement.GetProperty("payload").GetProperty("name").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task OnEntryAsync_WithTextFormat_WritesSanitizedSingleLineText ()
    {
        var standardError = new StringWriter();
        var sink = new CliCommandProgressSink(
            CliStreamEntryFormat.Text,
            new CliStreamEntryWriter("sample.command", standardError),
            new EchoTextProjector());

        await sink.OnEntryAsync("sample.diagnostic", "line 1\nline 2\t\u001B", CancellationToken.None);

        Assert.Equal("sample.diagnostic: line 1\\nline 2\\t\\u001B" + Environment.NewLine, standardError.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task OnEntryAsync_WhenTextProjectorSuppressesEntry_WritesNothing ()
    {
        var standardError = new StringWriter();
        var sink = new CliCommandProgressSink(
            CliStreamEntryFormat.Text,
            new CliStreamEntryWriter("sample.command", standardError),
            new SuppressingTextProjector());

        await sink.OnEntryAsync("sample.started", new { value = true }, CancellationToken.None);

        Assert.Equal(string.Empty, standardError.ToString());
    }

    private static void AssertEntryEnvelope (
        JsonElement root,
        int sequence,
        string eventName)
    {
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("sample.command", root.GetProperty("command").GetString());
        Assert.StartsWith("sample.command-", root.GetProperty("streamId").GetString(), StringComparison.Ordinal);
        Assert.Equal(sequence, root.GetProperty("sequence").GetInt32());
        Assert.Equal("2026-03-05T10:30:00.0000000+00:00", root.GetProperty("timestamp").GetString());
        Assert.Equal(eventName, root.GetProperty("event").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
    }

    private sealed class EchoTextProjector : ICliCommandProgressTextProjector
    {
        public bool TryCreateTextEntry<TPayload> (
            string eventName,
            TPayload payload,
            out string text)
            where TPayload : notnull
        {
            text = CliProgressTextFormatter.CreateDelimitedEntry(eventName, ": ", payload);
            return true;
        }
    }

    private sealed class SuppressingTextProjector : ICliCommandProgressTextProjector
    {
        public bool TryCreateTextEntry<TPayload> (
            string eventName,
            TPayload payload,
            out string text)
            where TPayload : notnull
        {
            text = string.Empty;
            return false;
        }
    }

    private sealed class ThrowingTextProjector : ICliCommandProgressTextProjector
    {
        public bool TryCreateTextEntry<TPayload> (
            string eventName,
            TPayload payload,
            out string text)
            where TPayload : notnull
        {
            throw new InvalidOperationException("Text projector must not be used for JSON entries.");
        }
    }
}
