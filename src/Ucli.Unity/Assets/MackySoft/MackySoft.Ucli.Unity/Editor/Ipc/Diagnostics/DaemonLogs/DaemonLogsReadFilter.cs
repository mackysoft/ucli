using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents validated filter predicates for daemon-log read requests. </summary>
    internal sealed record DaemonLogsReadFilter (
        long? AfterSequence,
        int? Tail,
        DateTimeOffset? Since,
        DateTimeOffset? Until,
        IpcLogLevel? Level,
        string Query,
        IpcLogQueryTarget QueryTarget,
        string Category);
}
