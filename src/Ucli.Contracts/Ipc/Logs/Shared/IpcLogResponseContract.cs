namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Enforces cursor ordering shared by incremental log-read responses. </summary>
internal static class IpcLogResponseContract
{
    internal static void Validate<TEvent> (
        IReadOnlyList<TEvent> events,
        IpcLogCursor nextCursor,
        Func<TEvent, IpcLogCursor> getCursor,
        string parameterName)
    {
        var hasPreviousSequence = false;
        var previousSequence = 0L;
        for (var index = 0; index < events.Count; index++)
        {
            var cursor = getCursor(events[index]);
            if (cursor.StreamId != nextCursor.StreamId)
            {
                throw new ArgumentException(
                    $"Log event cursor at index {index} must belong to the next-cursor stream.",
                    parameterName);
            }

            if (cursor.Sequence >= nextCursor.Sequence)
            {
                throw new ArgumentException(
                    $"Log event cursor at index {index} must precede the next cursor.",
                    parameterName);
            }

            if (hasPreviousSequence && cursor.Sequence <= previousSequence)
            {
                throw new ArgumentException(
                    $"Log event cursor at index {index} must strictly advance from the preceding event.",
                    parameterName);
            }

            previousSequence = cursor.Sequence;
            hasPreviousSequence = true;
        }
    }
}
