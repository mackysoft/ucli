namespace MackySoft.Ucli.TestRun.Execution;

/// <summary> Represents Unity test-execution failure kinds. </summary>
internal enum UnityTestExecutionFailureKind
{
    /// <summary> Indicates Unity process failed to start. </summary>
    StartFailed = 0,

    /// <summary> Indicates Unity process timed out. </summary>
    TimedOut = 1,

    /// <summary> Indicates Unity process execution was canceled. </summary>
    Canceled = 2,

    /// <summary> Indicates Unity process exited with an unsupported exit code. </summary>
    AbnormalExit = 3,

    /// <summary> Indicates required artifacts were not produced. </summary>
    ArtifactMissing = 4,
}