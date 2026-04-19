namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Represents process execution status values. </summary>
internal enum ProcessRunStatus
{
    /// <summary> Indicates process started and exited. </summary>
    Exited = 0,

    /// <summary> Indicates process failed to start. </summary>
    StartFailed = 1,

    /// <summary> Indicates process execution exceeded timeout. </summary>
    TimedOut = 2,

    /// <summary> Indicates process execution was canceled by caller request. </summary>
    Canceled = 3,
}