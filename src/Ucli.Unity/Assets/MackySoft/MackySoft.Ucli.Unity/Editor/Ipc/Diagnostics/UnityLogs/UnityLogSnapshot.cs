using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one immutable Unity-log stream snapshot. </summary>
    /// <param name="NextCursor"> The cursor used for the next incremental read. </param>
    /// <param name="Events"> The current event sequence snapshot. </param>
    internal sealed record UnityLogSnapshot (
        IpcLogCursor NextCursor,
        IReadOnlyList<UnityLogEvent> Events);
}
