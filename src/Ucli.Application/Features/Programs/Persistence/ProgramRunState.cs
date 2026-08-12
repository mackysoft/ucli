namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Defines the durable state of one Program Run. </summary>
[VocabularyDefinition]
public enum ProgramRunState
{
    [VocabularyText("created")]
    Created,
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
