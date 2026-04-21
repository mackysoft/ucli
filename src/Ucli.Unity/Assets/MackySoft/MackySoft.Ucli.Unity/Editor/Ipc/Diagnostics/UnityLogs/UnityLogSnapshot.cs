using System.Collections.Generic;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one immutable Unity-log stream snapshot. </summary>
    /// <param name="StreamId"> The stream identifier. </param>
    /// <param name="NextCursor"> The cursor used for the next incremental read. </param>
    /// <param name="Events"> The current event sequence snapshot. </param>
    internal sealed record UnityLogSnapshot (
        string StreamId,
        string NextCursor,
        IReadOnlyList<UnityLogEvent> Events);
}