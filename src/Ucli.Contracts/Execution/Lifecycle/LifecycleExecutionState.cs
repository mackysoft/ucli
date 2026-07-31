namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Defines the closed public state vocabulary projected by Lifecycle Execution handlers.
/// </summary>
[VocabularyDefinition]
public enum LifecycleExecutionState
{
    /// <summary> The start record is durable and no action side effect has been issued. </summary>
    [VocabularyText("registered")]
    Registered = 1,

    /// <summary> A project refresh is active. </summary>
    [VocabularyText("refreshing")]
    Refreshing = 2,

    /// <summary> Script compilation is active. </summary>
    [VocabularyText("compiling")]
    Compiling = 3,

    /// <summary> A Play Mode entry is active. </summary>
    [VocabularyText("entering")]
    Entering = 4,

    /// <summary> A Play Mode exit is active. </summary>
    [VocabularyText("exiting")]
    Exiting = 5,

    /// <summary> The owning handler is recovering the same execution after connectivity was lost. </summary>
    [VocabularyText("recovering")]
    Recovering = 6,

    /// <summary> The owning handler is publishing and verifying the immutable terminal record. </summary>
    [VocabularyText("publishing")]
    Publishing = 7,

    /// <summary> The typed action result was finalized successfully. </summary>
    [VocabularyText("completed")]
    Completed = 8,

    /// <summary> A typed non-success terminal reason was finalized. </summary>
    [VocabularyText("failed")]
    Failed = 9,
}
