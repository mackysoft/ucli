namespace MackySoft.Ucli.Execution;

/// <summary> Defines the resolved execution target selected after mode decision. </summary>
internal enum UnityExecutionTarget
{
    /// <summary> Executes the request through Unity daemon IPC. </summary>
    Daemon = 0,

    /// <summary> Executes the request by launching Unity in oneshot batchmode. </summary>
    Oneshot = 1,
}