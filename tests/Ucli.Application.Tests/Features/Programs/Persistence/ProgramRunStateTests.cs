using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Persistence;

public sealed class ProgramRunStateTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(ProgramRunState.Created, ExecutionLifecycle.Active)]
    [InlineData(ProgramRunState.Running, ExecutionLifecycle.Active)]
    [InlineData(ProgramRunState.WaitingForRuntime, ExecutionLifecycle.Active)]
    [InlineData(ProgramRunState.Cancelling, ExecutionLifecycle.Recovery)]
    [InlineData(ProgramRunState.Completed, ExecutionLifecycle.Terminal)]
    [InlineData(ProgramRunState.Failed, ExecutionLifecycle.Terminal)]
    [InlineData(ProgramRunState.Cancelled, ExecutionLifecycle.Terminal)]
    [InlineData(ProgramRunState.Interrupted, ExecutionLifecycle.Terminal)]
    public void ToExecutionLifecycle_ProjectsEveryRunState (
        ProgramRunState state,
        ExecutionLifecycle expected)
    {
        Assert.Equal(expected, ProgramRunStateSemantics.ToExecutionLifecycle(state));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(ProgramRunState.Created, ProgramRunState.Running, true)]
    [InlineData(ProgramRunState.Created, ProgramRunState.Completed, true)]
    [InlineData(ProgramRunState.Created, ProgramRunState.Cancelled, false)]
    [InlineData(ProgramRunState.Running, ProgramRunState.WaitingForRuntime, true)]
    [InlineData(ProgramRunState.WaitingForRuntime, ProgramRunState.Running, true)]
    [InlineData(ProgramRunState.Running, ProgramRunState.Completed, true)]
    [InlineData(ProgramRunState.Completed, ProgramRunState.Running, false)]
    [InlineData(ProgramRunState.Completed, ProgramRunState.Completed, false)]
    public void CanTransitionTo_RejectsAnyTransitionAfterTerminalState (
        ProgramRunState from,
        ProgramRunState to,
        bool expected)
    {
        Assert.Equal(expected, ProgramRunStateSemantics.CanTransitionTo(from, to));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AggregateVerdict_DoesNotTreatExecutionFailureAsVerdictFailure ()
    {
        Assert.Equal(
            Verdict.Pass,
            ProgramRunStateSemantics.AggregateVerdict(
                ProgramRunState.Interrupted,
                [Verdict.Pass]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CancellationRecord_RejectsInconsistentPersistedFacts ()
    {
        Assert.Throws<ArgumentException>(() => new ProgramCancellationRecord(false, DateTimeOffset.UtcNow, null).Validate());
        Assert.Throws<ArgumentException>(() => new ProgramCancellationRecord(true, null, "USER_CANCELLED").Validate());
    }
}
