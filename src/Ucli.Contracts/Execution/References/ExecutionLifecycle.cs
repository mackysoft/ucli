namespace MackySoft.Ucli.Contracts;

/// <summary> Identifies whether a logical execution is progressing, recovering, or finalized. </summary>
[VocabularyDefinition]
public enum ExecutionLifecycle
{
    /// <summary> The feature can continue normal forward progress. </summary>
    [VocabularyText("active")]
    Active = 0,

    /// <summary> The feature is cancelling, reconnecting, or otherwise recovering before finalization. </summary>
    [VocabularyText("recovery")]
    Recovery,

    /// <summary> The feature has finalized its immutable terminal record. </summary>
    [VocabularyText("terminal")]
    Terminal,
}
