namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Identifies immutable artifacts published by the Lifecycle Execution feature. </summary>
[VocabularyDefinition]
public enum LifecycleExecutionArtifactKind
{
    /// <summary> The immutable record reverified before a terminal execution reference is published. </summary>
    [VocabularyText("lifecycleExecutionTerminalRecord")]
    TerminalRecord = 1,
}
