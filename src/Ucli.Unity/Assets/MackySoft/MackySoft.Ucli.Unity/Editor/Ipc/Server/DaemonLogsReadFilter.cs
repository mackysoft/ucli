using System;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents validated filter predicates for daemon-log read requests. </summary>
    internal sealed record DaemonLogsReadFilter (
        long? AfterSequence,
        int? Tail,
        DateTimeOffset? Since,
        DateTimeOffset? Until,
        string Level,
        string Query,
        string QueryTarget,
        string Category);
}
