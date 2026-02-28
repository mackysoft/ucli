namespace MackySoft.Ucli.Execution;

/// <summary> Defines the requested Unity execution mode from the <c>--mode</c> option contract. </summary>
internal enum UnityExecutionMode
{
    /// <summary> Uses daemon when reachable; otherwise falls back to oneshot. </summary>
    Auto = 0,

    /// <summary> Requires daemon execution and rejects requests when daemon is not reachable. </summary>
    Daemon = 1,

    /// <summary> Requires oneshot execution and rejects requests while daemon is reachable. </summary>
    Oneshot = 2,
}