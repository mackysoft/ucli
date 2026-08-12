using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Shared.Execution.Lifecycle;

public sealed class LifecycleExecutionWaitFailureTests
{
    [Theory]
    [InlineData(false, false, false, ExecutionApplicationState.NotApplied)]
    [InlineData(true, true, false, ExecutionApplicationState.Indeterminate)]
    [InlineData(true, false, false, ExecutionApplicationState.NotApplied)]
    [InlineData(true, false, true, ExecutionApplicationState.Indeterminate)]
    [Trait("Size", "Small")]
    public void Resolve_ClassifiesOnlyApplicationStateProvenAtWaitFailure (
        bool hasStart,
        bool isCallerCancellation,
        bool lifecycleActionDispatched,
        ExecutionApplicationState expected)
    {
        var start = CreateStart();

        var actual = LifecycleExecutionWaitFailure.Resolve(
            durableStartExecutionReference:
                hasStart ? start.LifecycleExecutionRef : null,
            isCallerCancellation: isCallerCancellation,
            lifecycleActionDispatched: lifecycleActionDispatched,
            establishedExecutionReference: null);

        Assert.Equal(expected, actual.ApplicationState);
        Assert.Equal(
            hasStart ? start.LifecycleExecutionRef : null,
            actual.ExecutionReference);
    }

    [Theory]
    [InlineData(false, false, ExecutionApplicationState.Unknown)]
    [InlineData(true, false, ExecutionApplicationState.Indeterminate)]
    [InlineData(false, true, ExecutionApplicationState.Indeterminate)]
    [Trait("Size", "Small")]
    public void Resolve_ForReconnect_RetainsAuthoritativeReferenceWithoutClaimingOriginalNonApplication (
        bool isCallerCancellation,
        bool lifecycleActionDispatched,
        ExecutionApplicationState expected)
    {
        var durableStartExecutionReference =
            CreateStart().LifecycleExecutionRef;
        var establishedReference = CreatePublishingReference();

        var actual = LifecycleExecutionWaitFailure.Resolve(
            durableStartExecutionReference:
                durableStartExecutionReference,
            isCallerCancellation: isCallerCancellation,
            lifecycleActionDispatched: lifecycleActionDispatched,
            establishedExecutionReference:
                establishedReference);

        Assert.Equal(expected, actual.ApplicationState);
        Assert.Equal(establishedReference, actual.ExecutionReference);
    }
}
