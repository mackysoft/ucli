namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Identifies the action owned by one Lifecycle Execution. </summary>
[VocabularyDefinition]
public enum LifecycleExecutionKind
{
    /// <summary> Refreshes the Unity project and recovers its resulting lifecycle state. </summary>
    [VocabularyText("refresh")]
    Refresh = 1,

    /// <summary> Refreshes, compiles, and recovers the resulting compile state. </summary>
    [VocabularyText("compile")]
    Compile = 2,

    /// <summary> Enters Play Mode and observes the terminal playing state. </summary>
    [VocabularyText("play.enter")]
    PlayEnter = 3,

    /// <summary> Exits Play Mode and observes the terminal runnable state. </summary>
    [VocabularyText("play.exit")]
    PlayExit = 4,
}
