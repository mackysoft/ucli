using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Tests.Execution.Lifecycle;

public sealed class LifecycleExecutionTerminalFactsPolicyTests
{
    [Theory]
    [InlineData(false, ExecutionLifecycle.Active, LifecycleExecutionState.Registered, ExecutionApplicationState.NotApplied)]
    [InlineData(true, ExecutionLifecycle.Active, LifecycleExecutionState.Registered, ExecutionApplicationState.Indeterminate)]
    [InlineData(false, ExecutionLifecycle.Active, LifecycleExecutionState.Refreshing, ExecutionApplicationState.Indeterminate)]
    [InlineData(false, ExecutionLifecycle.Recovery, LifecycleExecutionState.Recovering, ExecutionApplicationState.Indeterminate)]
    public void ResolveUnprovenApplicationState_UsesAdmissionAndDurableState (
        bool lifecycleActionAdmitted,
        ExecutionLifecycle lifecycle,
        LifecycleExecutionState state,
        ExecutionApplicationState expected)
    {
        var currentReference =
            LifecycleExecutionContractTestFactory.CreateReference(
                LifecycleExecutionKind.Refresh,
                lifecycle,
                state);

        var actual =
            LifecycleExecutionTerminalFactsPolicy
                .ResolveUnprovenApplicationState(
                    currentReference,
                    lifecycleActionAdmitted);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveHostExit_BeforeDeadline_ReportsUnityExitAndClampsCompletionToStart ()
    {
        var start = LifecycleExecutionContractTestFactory.CreateStart(
            LifecycleExecutionKind.Compile);

        var actual =
            LifecycleExecutionTerminalFactsPolicy.ResolveHostExit(
                start,
                start.LifecycleExecutionRef,
                lifecycleActionAdmitted: true,
                start.StartedAtUtc.AddMinutes(-1));

        Assert.Equal(
            LifecycleExecutionTerminalReason.UnityExited,
            actual.TerminalReason);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            actual.ApplicationState);
        Assert.Equal(start.StartedAtUtc, actual.CompletedAtUtc);
    }

    [Fact]
    public void ResolveHostExit_AtDeadline_ReportsDeadlineExceeded ()
    {
        var start = LifecycleExecutionContractTestFactory.CreateStart(
            LifecycleExecutionKind.PlayEnter);

        var actual =
            LifecycleExecutionTerminalFactsPolicy.ResolveHostExit(
                start,
                start.LifecycleExecutionRef,
                lifecycleActionAdmitted: false,
                start.DeadlineUtc);

        Assert.Equal(
            LifecycleExecutionTerminalReason.DeadlineExceeded,
            actual.TerminalReason);
        Assert.Equal(
            ExecutionApplicationState.NotApplied,
            actual.ApplicationState);
        Assert.Equal(start.DeadlineUtc, actual.CompletedAtUtc);
    }

    [Fact]
    public void ResolveTerminalFacts_BeforeDeadline_PreservesAttributableFactsAndNormalizesUtc ()
    {
        var start = LifecycleExecutionContractTestFactory.CreateStart(
            LifecycleExecutionKind.Refresh);
        var completedAtUtc = start.StartedAtUtc.AddSeconds(1);

        var actual =
            LifecycleExecutionTerminalFactsPolicy.ResolveTerminalFacts(
                start,
                LifecycleExecutionTerminalReason.Completed,
                ExecutionApplicationState.Applied,
                start.StartedGeneration,
                completedAtUtc.ToOffset(TimeSpan.FromHours(9)));

        Assert.Equal(
            LifecycleExecutionTerminalReason.Completed,
            actual.TerminalReason);
        Assert.Equal(
            ExecutionApplicationState.Applied,
            actual.ApplicationState);
        Assert.Same(
            start.StartedGeneration,
            actual.TerminalGeneration);
        Assert.Equal(completedAtUtc, actual.CompletedAtUtc);
        Assert.Equal(TimeSpan.Zero, actual.CompletedAtUtc.Offset);
    }

    [Theory]
    [MemberData(nameof(RegressingTerminalGenerations))]
    public void ResolveTerminalFacts_WhenAnyGenerationRegresses_ReportsGenerationMismatch (
        UnityEditorGenerationSnapshot terminalGeneration)
    {
        var start = LifecycleExecutionContractTestFactory.CreateStart(
            LifecycleExecutionKind.Compile);

        var actual =
            LifecycleExecutionTerminalFactsPolicy.ResolveTerminalFacts(
                start,
                LifecycleExecutionTerminalReason.Completed,
                ExecutionApplicationState.Indeterminate,
                terminalGeneration,
                start.StartedAtUtc.AddSeconds(1));

        Assert.Equal(
            LifecycleExecutionTerminalReason.GenerationMismatch,
            actual.TerminalReason);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            actual.ApplicationState);
        Assert.Null(actual.TerminalGeneration);
    }

    [Theory]
    [InlineData(LifecycleExecutionTerminalReason.ProjectMismatch)]
    [InlineData(LifecycleExecutionTerminalReason.HostMismatch)]
    [InlineData(LifecycleExecutionTerminalReason.GenerationMismatch)]
    [InlineData(LifecycleExecutionTerminalReason.UnityExited)]
    public void ResolveTerminalFacts_WhenReasonCannotAttributeGeneration_ClearsGeneration (
        LifecycleExecutionTerminalReason terminalReason)
    {
        var start = LifecycleExecutionContractTestFactory.CreateStart(
            LifecycleExecutionKind.PlayExit);

        var actual =
            LifecycleExecutionTerminalFactsPolicy.ResolveTerminalFacts(
                start,
                terminalReason,
                ExecutionApplicationState.Indeterminate,
                LifecycleExecutionContractTestFactory.TerminalGeneration,
                start.StartedAtUtc.AddSeconds(1));

        Assert.Equal(terminalReason, actual.TerminalReason);
        Assert.Null(actual.TerminalGeneration);
    }

    [Fact]
    public void ResolveTerminalFacts_AtDeadline_DeadlineWinsWithoutRestoringUnattributedGeneration ()
    {
        var start = LifecycleExecutionContractTestFactory.CreateStart(
            LifecycleExecutionKind.PlayEnter);

        var actual =
            LifecycleExecutionTerminalFactsPolicy.ResolveTerminalFacts(
                start,
                LifecycleExecutionTerminalReason.HostMismatch,
                ExecutionApplicationState.Indeterminate,
                LifecycleExecutionContractTestFactory.TerminalGeneration,
                start.DeadlineUtc);

        Assert.Equal(
            LifecycleExecutionTerminalReason.DeadlineExceeded,
            actual.TerminalReason);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            actual.ApplicationState);
        Assert.Null(actual.TerminalGeneration);
        Assert.Equal(start.DeadlineUtc, actual.CompletedAtUtc);
    }

    public static TheoryData<UnityEditorGenerationSnapshot>
        RegressingTerminalGenerations => new()
        {
            new UnityEditorGenerationSnapshot(9, 20, 30, 40),
            new UnityEditorGenerationSnapshot(10, 19, 30, 40),
            new UnityEditorGenerationSnapshot(10, 20, 29, 40),
            new UnityEditorGenerationSnapshot(10, 20, 30, 39),
        };
}
