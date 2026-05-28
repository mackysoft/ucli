using MackySoft.Ucli.Infrastructure.Text;

namespace MackySoft.Ucli.Hosting.Cli.Common.Streaming;

/// <summary> Formats small progress text entries with caller-specified delimiters. </summary>
internal static class CliProgressTextFormatter
{
    /// <summary> Creates one text entry by joining an event name and payload text with a delimiter. </summary>
    /// <typeparam name="TPayload"> The concrete event payload type. </typeparam>
    public static string CreateDelimitedEntry<TPayload> (
        string eventName,
        string delimiter,
        TPayload payload)
        where TPayload : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (delimiter == null)
        {
            throw new ArgumentNullException(nameof(delimiter));
        }

        var payloadText = payload.ToString() ?? string.Empty;
        var length = checked(eventName.Length + delimiter.Length + payloadText.Length);
        return string.Create(
            length,
            (eventName, delimiter, payloadText),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(state.eventName);
                writer.Append(state.delimiter);
                writer.Append(state.payloadText);
            });
    }
}
