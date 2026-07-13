using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class IpcEditorLifecycleSemanticsTests
{
    public static TheoryData<IpcEditorLifecycleState, IpcEditorBlockingReason?, bool> DefinedLifecycleTuples => new()
    {
        { IpcEditorLifecycleState.Starting, IpcEditorBlockingReason.Startup, false },
        { IpcEditorLifecycleState.Recovering, IpcEditorBlockingReason.Recovery, false },
        { IpcEditorLifecycleState.Ready, null, true },
        { IpcEditorLifecycleState.Busy, IpcEditorBlockingReason.Busy, false },
        { IpcEditorLifecycleState.Compiling, IpcEditorBlockingReason.Compile, false },
        { IpcEditorLifecycleState.CompileFailed, IpcEditorBlockingReason.CompileFailed, false },
        { IpcEditorLifecycleState.DomainReloading, IpcEditorBlockingReason.DomainReload, false },
        { IpcEditorLifecycleState.Reimporting, IpcEditorBlockingReason.Reimport, false },
        { IpcEditorLifecycleState.PlayMode, IpcEditorBlockingReason.PlayMode, false },
        { IpcEditorLifecycleState.ModalBlocked, IpcEditorBlockingReason.ModalDialog, false },
        { IpcEditorLifecycleState.SafeMode, IpcEditorBlockingReason.SafeMode, false },
        { IpcEditorLifecycleState.ShuttingDown, IpcEditorBlockingReason.Shutdown, false },
        { IpcEditorLifecycleState.Unavailable, IpcEditorBlockingReason.Unavailable, false },
    };

    public static TheoryData<IpcEditorLifecycleState, IpcEditorBlockingReason?, bool> InconsistentLifecycleTuples => new()
    {
        { IpcEditorLifecycleState.Ready, null, false },
        { IpcEditorLifecycleState.Ready, IpcEditorBlockingReason.Busy, true },
        { IpcEditorLifecycleState.Compiling, null, false },
        { IpcEditorLifecycleState.Compiling, IpcEditorBlockingReason.Busy, false },
        { IpcEditorLifecycleState.Compiling, IpcEditorBlockingReason.Compile, true },
        { (IpcEditorLifecycleState)(-1), null, false },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(DefinedLifecycleTuples))]
    public void Resolve_WhenLifecycleStateIsDefined_ReturnsExpectedTuple (
        IpcEditorLifecycleState lifecycleState,
        IpcEditorBlockingReason? expectedBlockingReason,
        bool expectedCanAcceptExecutionRequests)
    {
        var blockingReason = IpcEditorLifecycleSemantics.ResolveBlockingReason(lifecycleState);
        var canAcceptExecutionRequests = IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(lifecycleState);

        Assert.Equal(expectedBlockingReason, blockingReason);
        Assert.Equal(expectedCanAcceptExecutionRequests, canAcceptExecutionRequests);
        Assert.True(IpcEditorLifecycleSemantics.IsConsistent(
            lifecycleState,
            blockingReason,
            canAcceptExecutionRequests));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DefinedLifecycleTuples_CoverEveryLifecycleState ()
    {
        var coveredStates = DefinedLifecycleTuples.Select(static values => (IpcEditorLifecycleState)values[0]);

        Assert.Equal(Enum.GetValues<IpcEditorLifecycleState>(), coveredStates);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InconsistentLifecycleTuples))]
    public void IsConsistent_WhenTupleDoesNotMatchLifecycleState_ReturnsFalse (
        IpcEditorLifecycleState lifecycleState,
        IpcEditorBlockingReason? blockingReason,
        bool canAcceptExecutionRequests)
    {
        var result = IpcEditorLifecycleSemantics.IsConsistent(
            lifecycleState,
            blockingReason,
            canAcceptExecutionRequests);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenLifecycleStateIsUndefined_ThrowsArgumentOutOfRangeException ()
    {
        var lifecycleState = (IpcEditorLifecycleState)(-1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => IpcEditorLifecycleSemantics.ResolveBlockingReason(lifecycleState));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(lifecycleState));
    }
}
