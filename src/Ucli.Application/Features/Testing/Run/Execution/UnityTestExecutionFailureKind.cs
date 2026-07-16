namespace MackySoft.Ucli.Application.Features.Testing.Run.Execution;

/// <summary> Represents Unity test-execution failure kinds. </summary>
internal enum UnityTestExecutionFailureKind
{
    /// <summary> Indicates Unity process failed to start. </summary>
    StartFailed = 0,

    /// <summary> Indicates daemon IPC transport timed out. </summary>
    IpcTimedOut = 1,

    /// <summary> Indicates Unity process execution timed out. </summary>
    ProcessTimedOut = 2,

    /// <summary> Indicates Unity process execution was canceled. </summary>
    Canceled = 3,

    /// <summary> Indicates Unity process exited with an unsupported exit code. </summary>
    AbnormalExit = 4,

    /// <summary> Indicates required artifacts were not produced. </summary>
    ArtifactMissing = 5,

    /// <summary> Indicates client-side daemon execution setup failed before Unity request dispatch. </summary>
    ClientSetupFailed = 6,

    /// <summary> Indicates the Unity project is already open or locked by another Unity process. </summary>
    ProjectAlreadyOpen = 7,

    /// <summary> Indicates streamed Unity test progress violated the public test-run progress contract. </summary>
    ProgressProtocolViolation = 8,

    /// <summary> Indicates the IPC transport ended before the complete response was read. </summary>
    IpcTransportInterrupted = 9,
}
