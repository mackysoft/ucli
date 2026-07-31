namespace MackySoft.Ucli.Contracts;

/// <summary> Defines machine-readable failures owned by the Lifecycle Execution common guarantees. </summary>
public static class LifecycleExecutionErrorCodes
{
    /// <summary> Gets the code for reusing one execution identity with a different definition digest. </summary>
    public static readonly UcliCode DefinitionConflict =
        new("LIFECYCLE_EXECUTION_DEFINITION_CONFLICT");

    /// <summary> Gets the code for reconnecting a Lifecycle Execution to a different project. </summary>
    public static readonly UcliCode ProjectMismatch =
        new("LIFECYCLE_EXECUTION_PROJECT_MISMATCH");

    /// <summary> Gets the code for reconnecting a Lifecycle Execution to a different Editor host. </summary>
    public static readonly UcliCode HostMismatch =
        new("LIFECYCLE_EXECUTION_HOST_MISMATCH");

    /// <summary> Gets the code emitted when the fixed Unity Editor process exits before completion. </summary>
    public static readonly UcliCode UnityExited =
        new("LIFECYCLE_EXECUTION_UNITY_EXITED");

    /// <summary> Gets the code for an unproven endpoint or regressing Editor generation. </summary>
    public static readonly UcliCode GenerationMismatch =
        new("LIFECYCLE_EXECUTION_GENERATION_MISMATCH");

    /// <summary> Gets the code emitted after the immutable execution deadline is reached. </summary>
    public static readonly UcliCode DeadlineExceeded =
        new("LIFECYCLE_EXECUTION_DEADLINE_EXCEEDED");

    /// <summary> Gets the code for terminal-record publication or reverification failure. </summary>
    public static readonly UcliCode TerminalPublicationFailed =
        new("LIFECYCLE_EXECUTION_TERMINAL_PUBLICATION_FAILED");

    internal static IReadOnlyList<UcliCode> AllExcept (UcliCode code)
    {
        if (code is null)
        {
            throw new ArgumentNullException(nameof(code));
        }
        UcliCode[] knownCodes =
        [
            DefinitionConflict,
            ProjectMismatch,
            HostMismatch,
            GenerationMismatch,
            DeadlineExceeded,
            UnityExited,
            TerminalPublicationFailed,
        ];
        return knownCodes
            .Where(candidate => candidate != code)
            .ToArray();
    }
}
