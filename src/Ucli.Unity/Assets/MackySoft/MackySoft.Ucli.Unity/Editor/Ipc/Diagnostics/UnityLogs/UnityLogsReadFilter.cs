using System;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents validated filter predicates for Unity-log read requests. </summary>
    internal sealed record UnityLogsReadFilter (
        long? AfterSequence,
        int? Tail,
        DateTimeOffset? Since,
        DateTimeOffset? Until,
        string Level,
        string Query,
        string QueryTarget,
        string Source,
        string StackTraceMode,
        int? StackTraceMaxFrames,
        int? StackTraceMaxChars);
}