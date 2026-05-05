namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Defines how redirected process output streams are drained after process exit. </summary>
internal enum ProcessOutputDrainMode
{
    /// <summary> Waits for redirected output streams to reach end-of-stream before returning. </summary>
    WaitForCompletion = 0,

    /// <summary> Does not require redirected output streams to reach end-of-stream after the parent process exits. </summary>
    BestEffort = 1,
}
