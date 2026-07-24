
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines normalized build log completion reason literals. </summary>
[VocabularyDefinition]
public enum IpcBuildLogCompletionReason
{
    /// <summary> BuildPipeline completed successfully. </summary>
    [VocabularyText("completed")]
    Completed = 1,

    /// <summary> BuildPipeline completed with a failure result. </summary>
    [VocabularyText("failed")]
    Failed = 2,

    /// <summary> BuildPipeline completed with a canceled result. </summary>
    [VocabularyText("canceled")]
    Canceled = 3,
}
