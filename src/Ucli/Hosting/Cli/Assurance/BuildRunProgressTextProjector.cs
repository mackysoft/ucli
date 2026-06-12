using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Projects build run progress payloads into human-readable text entries. </summary>
internal sealed class BuildRunProgressTextProjector : ICliCommandProgressTextProjector
{
    /// <inheritdoc />
    public bool TryCreateTextEntry<TPayload> (
        string eventName,
        TPayload payload,
        out string text)
        where TPayload : notnull
    {
        text = CliProgressTextFormatter.CreateDelimitedEntry(eventName, " ", payload);
        return true;
    }
}
