namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Defines the shared execution-reference vocabulary for GameView recording.</summary>
public static class GameViewRecordingExecutionContract
{
    public static ExecutionKind Kind { get; } = new("gameViewRecording");

    public static ExecutionState Preparing { get; } = new("preparing");

    public static ExecutionState Recording { get; } = new("recording");

    public static ExecutionState Finalizing { get; } = new("finalizing");

    public static ExecutionState Completed { get; } = new("completed");

    public static ExecutionState Failed { get; } = new("failed");

    public static ExecutionState Indeterminate { get; } = new("indeterminate");

    /// <summary>Creates the feature-owned execution state for one recording state.</summary>
    public static ExecutionState ToExecutionState (GameViewRecordingState state)
    {
        EnsureDefined(state);
        return new ExecutionState(TextVocabulary.GetText(state));
    }

    /// <summary>Gets the common lifecycle owned by one recording state.</summary>
    public static ExecutionLifecycle GetLifecycle (GameViewRecordingState state)
    {
        EnsureDefined(state);
        return state switch
        {
            GameViewRecordingState.Preparing or GameViewRecordingState.Recording => ExecutionLifecycle.Active,
            GameViewRecordingState.Finalizing => ExecutionLifecycle.Recovery,
            GameViewRecordingState.Completed
                or GameViewRecordingState.Failed
                or GameViewRecordingState.Indeterminate => ExecutionLifecycle.Terminal,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Recording state must be defined."),
        };
    }

    private static void EnsureDefined (GameViewRecordingState state)
    {
        if (!TextVocabulary.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Recording state must be defined.");
        }
    }
}
