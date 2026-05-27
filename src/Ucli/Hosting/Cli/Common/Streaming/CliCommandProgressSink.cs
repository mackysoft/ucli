using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Hosting.Cli.Common.Streaming;

/// <summary> Projects command progress entries to the public CLI entry stream. </summary>
internal sealed class CliCommandProgressSink : ICommandProgressSink
{
    private readonly CliStreamEntryFormat format;
    private readonly CliStreamEntryWriter entryWriter;
    private readonly ICliCommandProgressTextProjector textProjector;

    /// <summary> Initializes a new instance of the <see cref="CliCommandProgressSink" /> class. </summary>
    public CliCommandProgressSink (
        CliStreamEntryFormat format,
        CliStreamEntryWriter entryWriter,
        ICliCommandProgressTextProjector textProjector)
    {
        this.format = format;
        this.entryWriter = entryWriter ?? throw new ArgumentNullException(nameof(entryWriter));
        this.textProjector = textProjector ?? throw new ArgumentNullException(nameof(textProjector));
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

        if (textProjector.TryCreateTextEntry(eventName, payload, out var text))
        {
            entryWriter.WriteTextEntry(text);
        }

        return ValueTask.CompletedTask;
    }
}
