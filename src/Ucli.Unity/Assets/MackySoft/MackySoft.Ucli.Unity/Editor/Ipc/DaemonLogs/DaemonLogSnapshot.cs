using System.Collections.Generic;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one immutable daemon-log stream snapshot. </summary>
    /// <param name="StreamId"> The daemon stream identifier. </param>
    /// <param name="NextCursor"> The cursor used for the next incremental read. </param>
    /// <param name="Events"> The current event sequence snapshot. </param>
    internal sealed record DaemonLogSnapshot (
        string StreamId,
        string NextCursor,
        IReadOnlyList<DaemonLogEvent> Events);
}
