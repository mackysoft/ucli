using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Tests.Recording;

public sealed class GameViewRecordingExecutionContractTests
{
    public static TheoryData<GameViewRecordingState, ExecutionState, ExecutionLifecycle> DefinedStates => new()
    {
        {
            GameViewRecordingState.Preparing,
            GameViewRecordingExecutionContract.Preparing,
            ExecutionLifecycle.Active
        },
        {
            GameViewRecordingState.Recording,
            GameViewRecordingExecutionContract.Recording,
            ExecutionLifecycle.Active
        },
        {
            GameViewRecordingState.Finalizing,
            GameViewRecordingExecutionContract.Finalizing,
            ExecutionLifecycle.Recovery
        },
        {
            GameViewRecordingState.Completed,
            GameViewRecordingExecutionContract.Completed,
            ExecutionLifecycle.Terminal
        },
        {
            GameViewRecordingState.Failed,
            GameViewRecordingExecutionContract.Failed,
            ExecutionLifecycle.Terminal
        },
        {
            GameViewRecordingState.Indeterminate,
            GameViewRecordingExecutionContract.Indeterminate,
            ExecutionLifecycle.Terminal
        },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(DefinedStates))]
    public void DefinedState_MapsToItsExecutionStateAndLifecycle (
        GameViewRecordingState state,
        ExecutionState expectedExecutionState,
        ExecutionLifecycle expectedLifecycle)
    {
        Assert.Equal(expectedExecutionState, GameViewRecordingExecutionContract.ToExecutionState(state));
        Assert.Equal(expectedLifecycle, GameViewRecordingExecutionContract.GetLifecycle(state));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DefinedStates_CoverEveryRecordingState ()
    {
        var coveredStates = DefinedStates.Select(static values => (GameViewRecordingState)values[0]);

        Assert.Equal(Enum.GetValues<GameViewRecordingState>(), coveredStates);
    }
}
