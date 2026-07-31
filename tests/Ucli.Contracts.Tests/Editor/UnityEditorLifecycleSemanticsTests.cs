using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UnityEditorLifecycleSemanticsTests
{
    public static TheoryData<UnityEditorLifecycleState, UnityEditorBlockingReason?, bool> DefinedLifecycleTuples => new()
    {
        { UnityEditorLifecycleState.Starting, UnityEditorBlockingReason.Startup, false },
        { UnityEditorLifecycleState.Recovering, UnityEditorBlockingReason.Recovery, false },
        { UnityEditorLifecycleState.Ready, null, true },
        { UnityEditorLifecycleState.Busy, UnityEditorBlockingReason.Busy, false },
        { UnityEditorLifecycleState.Compiling, UnityEditorBlockingReason.Compile, false },
        { UnityEditorLifecycleState.CompileFailed, UnityEditorBlockingReason.CompileFailed, false },
        { UnityEditorLifecycleState.DomainReloading, UnityEditorBlockingReason.DomainReload, false },
        { UnityEditorLifecycleState.Reimporting, UnityEditorBlockingReason.Reimport, false },
        { UnityEditorLifecycleState.PlayMode, UnityEditorBlockingReason.PlayMode, false },
        { UnityEditorLifecycleState.ModalBlocked, UnityEditorBlockingReason.ModalDialog, false },
        { UnityEditorLifecycleState.SafeMode, UnityEditorBlockingReason.SafeMode, false },
        { UnityEditorLifecycleState.ShuttingDown, UnityEditorBlockingReason.Shutdown, false },
        { UnityEditorLifecycleState.Unavailable, UnityEditorBlockingReason.Unavailable, false },
    };

    public static TheoryData<UnityEditorLifecycleState, UnityEditorBlockingReason?, bool> InconsistentLifecycleTuples => new()
    {
        { UnityEditorLifecycleState.Ready, null, false },
        { UnityEditorLifecycleState.Ready, UnityEditorBlockingReason.Busy, true },
        { UnityEditorLifecycleState.Compiling, null, false },
        { UnityEditorLifecycleState.Compiling, UnityEditorBlockingReason.Busy, false },
        { UnityEditorLifecycleState.Compiling, UnityEditorBlockingReason.Compile, true },
        { (UnityEditorLifecycleState)(-1), null, false },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(DefinedLifecycleTuples))]
    public void Resolve_WhenLifecycleStateIsDefined_ReturnsExpectedTuple (
        UnityEditorLifecycleState lifecycleState,
        UnityEditorBlockingReason? expectedBlockingReason,
        bool expectedCanAcceptExecutionRequests)
    {
        var blockingReason = UnityEditorLifecycleSemantics.ResolveBlockingReason(lifecycleState);
        var canAcceptExecutionRequests = UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(lifecycleState);

        Assert.Equal(expectedBlockingReason, blockingReason);
        Assert.Equal(expectedCanAcceptExecutionRequests, canAcceptExecutionRequests);
        Assert.True(UnityEditorLifecycleSemantics.IsConsistent(
            lifecycleState,
            blockingReason,
            canAcceptExecutionRequests));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DefinedLifecycleTuples_CoverEveryLifecycleState ()
    {
        var coveredStates = DefinedLifecycleTuples.Select(static values => (UnityEditorLifecycleState)values[0]);

        Assert.Equal(Enum.GetValues<UnityEditorLifecycleState>(), coveredStates);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InconsistentLifecycleTuples))]
    public void IsConsistent_WhenTupleDoesNotMatchLifecycleState_ReturnsFalse (
        UnityEditorLifecycleState lifecycleState,
        UnityEditorBlockingReason? blockingReason,
        bool canAcceptExecutionRequests)
    {
        var result = UnityEditorLifecycleSemantics.IsConsistent(
            lifecycleState,
            blockingReason,
            canAcceptExecutionRequests);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenLifecycleStateIsUndefined_ThrowsArgumentOutOfRangeException ()
    {
        var lifecycleState = (UnityEditorLifecycleState)(-1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => UnityEditorLifecycleSemantics.ResolveBlockingReason(lifecycleState));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(lifecycleState));
    }
}
