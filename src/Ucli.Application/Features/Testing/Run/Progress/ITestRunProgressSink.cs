namespace MackySoft.Ucli.Application.Features.Testing.Run.Progress;

/// <summary> Receives public test-run progress entries from the execution pipeline. </summary>
internal interface ITestRunProgressSink
{
    /// <summary> Emits one progress entry. </summary>
    /// <param name="eventName"> The event name from the closed <c>test.run</c> stream set. </param>
    /// <param name="payload"> The event payload. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that completes after the entry is accepted. </returns>
    ValueTask OnEntryAsync (
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);
}
