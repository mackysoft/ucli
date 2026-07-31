namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Identifies why one Lifecycle Execution reached its terminal state. </summary>
[VocabularyDefinition]
public enum LifecycleExecutionTerminalReason
{
    /// <summary> The action-specific completion condition and typed result were confirmed. </summary>
    [VocabularyText("completed")]
    Completed = 1,

    /// <summary> The action handler confirmed an explicit action failure. </summary>
    [VocabularyText("actionFailed")]
    ActionFailed = 2,

    /// <summary> The immutable execution deadline was reached. </summary>
    [VocabularyText("deadlineExceeded")]
    DeadlineExceeded = 3,

    /// <summary> A reconnecting project did not match the registered project. </summary>
    [VocabularyText("projectMismatch")]
    ProjectMismatch = 4,

    /// <summary> A reconnecting Editor did not match the registered process generation and instance. </summary>
    [VocabularyText("hostMismatch")]
    HostMismatch = 5,

    /// <summary> An endpoint or Editor generation did not satisfy the registered successor contract. </summary>
    [VocabularyText("generationMismatch")]
    GenerationMismatch = 6,

    /// <summary> The registered Unity Editor process was confirmed to have exited. </summary>
    [VocabularyText("unityExited")]
    UnityExited = 7,
}
