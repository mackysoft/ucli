namespace MackySoft.Ucli.Application.Shared.Execution.Progress;

/// <summary> Receives command-neutral public progress entries from application workflows. </summary>
internal interface ICommandProgressSink
{
    /// <summary> Emits one progress entry. </summary>
    /// <param name="eventName"> The command-specific event name. </param>
    /// <param name="payload"> The event payload. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that completes after the entry is accepted. </returns>
    ValueTask OnEntryAsync (
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);
}
