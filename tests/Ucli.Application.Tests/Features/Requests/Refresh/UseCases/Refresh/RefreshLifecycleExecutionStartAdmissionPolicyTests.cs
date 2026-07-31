using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Refresh;

public sealed class RefreshLifecycleExecutionStartAdmissionPolicyTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityEditorLifecycleState.Starting)]
    [InlineData(UnityEditorLifecycleState.Busy)]
    [InlineData(UnityEditorLifecycleState.Compiling)]
    public void Evaluate_WhenDefaultStateIsWaitable_ReturnsWait (
        UnityEditorLifecycleState lifecycleState)
    {
        var policy = new RefreshLifecycleExecutionStartAdmissionPolicy(
            failFast: false);

        var decision = policy.Evaluate(
            UnityEditorObservationTestFactory.Create(lifecycleState));

        Assert.False(decision.IsReady);
        Assert.False(decision.IsFailure);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityEditorLifecycleState.Recovering)]
    [InlineData(UnityEditorLifecycleState.Reimporting)]
    public void Evaluate_WhenGenericWaitableStateIsOutsideRefreshWaitSet_ReturnsTypedFailure (
        UnityEditorLifecycleState lifecycleState)
    {
        var policy = new RefreshLifecycleExecutionStartAdmissionPolicy(
            failFast: false);

        var decision = policy.Evaluate(
            UnityEditorObservationTestFactory.Create(lifecycleState));

        Assert.True(decision.IsFailure);
        Assert.Equal(
            lifecycleState == UnityEditorLifecycleState.Recovering
                ? EditorLifecycleErrorCodes.EditorRecovering
                : EditorLifecycleErrorCodes.EditorReimporting,
            decision.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Evaluate_WhenFailFastStateWouldOtherwiseWait_ReturnsTypedFailure ()
    {
        var policy = new RefreshLifecycleExecutionStartAdmissionPolicy(
            failFast: true);

        var decision = policy.Evaluate(
            UnityEditorObservationTestFactory.Create(
                UnityEditorLifecycleState.Busy));

        Assert.True(decision.IsFailure);
        Assert.Equal(EditorLifecycleErrorCodes.EditorBusy, decision.ErrorCode);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityEditorLifecycleState.Starting)]
    [InlineData(UnityEditorLifecycleState.Busy)]
    [InlineData(UnityEditorLifecycleState.Compiling)]
    public void ShouldRetryAfterRejectedStart_WhenDefaultErrorIsWaitable_ReturnsTrue (
        UnityEditorLifecycleState lifecycleState)
    {
        var policy = new RefreshLifecycleExecutionStartAdmissionPolicy(
            failFast: false);
        var errorCode = lifecycleState switch
        {
            UnityEditorLifecycleState.Starting =>
                EditorLifecycleErrorCodes.EditorStarting,
            UnityEditorLifecycleState.Busy =>
                EditorLifecycleErrorCodes.EditorBusy,
            UnityEditorLifecycleState.Compiling =>
                EditorLifecycleErrorCodes.EditorCompiling,
            _ => throw new ArgumentOutOfRangeException(
                nameof(lifecycleState),
                lifecycleState,
                "The test state must identify a refresh waitable error."),
        };

        Assert.True(policy.ShouldRetryAfterRejectedStart(errorCode));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ShouldRetryAfterRejectedStart_WhenFailFastOrErrorIsNotWaitable_ReturnsFalse ()
    {
        Assert.False(
            new RefreshLifecycleExecutionStartAdmissionPolicy(failFast: true)
                .ShouldRetryAfterRejectedStart(
                    EditorLifecycleErrorCodes.EditorBusy));
        Assert.False(
            new RefreshLifecycleExecutionStartAdmissionPolicy(failFast: false)
                .ShouldRetryAfterRejectedStart(
                    EditorLifecycleErrorCodes.EditorRecovering));
    }
}
