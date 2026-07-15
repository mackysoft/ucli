using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents validated filter predicates for Unity-log read requests. </summary>
    internal sealed record UnityLogsReadFilter (
        long? AfterSequence,
        int? Tail,
        DateTimeOffset? Since,
        DateTimeOffset? Until,
        IpcLogLevel? Level,
        string Query,
        IpcLogQueryTarget QueryTarget,
        IpcUnityLogSource? Source,
        IpcUnityLogStackTraceMode StackTraceMode,
        int? StackTraceMaxFrames,
        int? StackTraceMaxChars);
}
