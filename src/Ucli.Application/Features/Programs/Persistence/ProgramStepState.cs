namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Defines the durable state of one Program Step. </summary>
[VocabularyDefinition]
public enum ProgramStepState
{
    [VocabularyText("deferred")]
    Deferred,
    [VocabularyText("planning")]
    Planning,
    [VocabularyText("running")]
    Running,
    [VocabularyText("waitingForRuntime")]
    WaitingForRuntime,
    [VocabularyText("cancelling")]
    Cancelling,
    [VocabularyText("completed")]
    Completed,
    [VocabularyText("failed")]
    Failed,
    [VocabularyText("cancelled")]
    Cancelled,
    [VocabularyText("interrupted")]
    Interrupted,
}
