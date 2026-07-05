namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal static class UnityExecutionModeDecisionServiceAssert
{
    public static StubModeDecisionService.Invocation DecisionAttemptedWithTimeout (
        StubModeDecisionService modeDecisionService,
        TimeSpan expectedTimeout)
    {
        var invocation = Assert.Single(modeDecisionService.Invocations);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        return invocation;
    }
}
