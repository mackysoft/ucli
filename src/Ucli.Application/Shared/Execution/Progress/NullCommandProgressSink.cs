namespace MackySoft.Ucli.Application.Shared.Execution.Progress;

/// <summary> Ignores command progress entries when a caller does not require streaming output. </summary>
internal sealed class NullCommandProgressSink : ICommandProgressSink
{
    /// <summary> Gets the shared no-op progress sink. </summary>
    public static NullCommandProgressSink Instance { get; } = new();

    private NullCommandProgressSink ()
    {
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
        return ValueTask.CompletedTask;
    }
}
