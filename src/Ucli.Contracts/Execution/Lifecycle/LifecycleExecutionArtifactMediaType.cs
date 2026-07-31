namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Identifies media types emitted by the Lifecycle Execution feature. </summary>
[VocabularyDefinition]
public enum LifecycleExecutionArtifactMediaType
{
    /// <summary> JSON encoded according to the published terminal-record schema. </summary>
    [VocabularyText("application/json")]
    Json = 1,
}
