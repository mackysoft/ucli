namespace MackySoft.Ucli.Application.Features.Testing.Run.Execution;

/// <summary> Represents Unity test-execution failure kinds. </summary>
internal enum UnityTestExecutionFailureKind
{
    /// <summary> Indicates Unity process failed to start. </summary>
    StartFailed = 1,

    /// <summary> Indicates daemon IPC transport timed out. </summary>
    IpcTimedOut = 2,

    /// <summary> Indicates Unity process execution timed out. </summary>
    ProcessTimedOut = 3,

    /// <summary> Indicates Unity process execution was canceled. </summary>
    Canceled = 4,

    /// <summary> Indicates Unity process exited with an unsupported exit code. </summary>
    AbnormalExit = 5,

    /// <summary> Indicates required artifacts were not produced. </summary>
    ArtifactMissing = 6,

    /// <summary> Indicates streamed Unity test progress violated the public test-run progress contract. </summary>
    ProgressProtocolViolation = 7,

    /// <summary> Indicates the IPC transport ended before the complete response was read. </summary>
    IpcTransportInterrupted = 8,

    /// <summary> Indicates the Unity request boundary reported a failure without process-exit evidence. </summary>
    RequestFailed = 9,

    /// <summary> Indicates the Unity response did not satisfy the test-run response contract. </summary>
    InvalidResponse = 10,

    /// <summary> Indicates an unexpected internal failure while executing the Unity request. </summary>
    InternalError = 11,

    /// <summary> Indicates the executed operation rejected invalid test-run input. </summary>
    InvalidArgument = 12,
}
