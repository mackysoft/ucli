using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;

namespace MackySoft.Ucli.Application.Tests;

internal static class PlanServiceInvocationAssert
{
    public static RecordingRequestStaticValidationService.Invocation AllowPlayModeUsedLiveStaticValidation (
        PlanServiceResult result,
        RecordingRequestStaticValidationPreflightService staticPreflightService,
        RecordingRequestStaticValidationService staticValidationService)
    {
        Assert.True(result.IsSuccess);
        Assert.Empty(staticPreflightService.Invocations);
        var validationInvocation = Assert.Single(staticValidationService.Invocations);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        Assert.False(result.Output.ReadIndex.Hit);
        Assert.Equal(ReadIndexInfoSource.Unity, result.Output.ReadIndex.Source);
        Assert.Equal("Play Mode mutation uses live Unity state.", result.Output.ReadIndex.FallbackReason);
        return validationInvocation;
    }

    public static void ReadIndexModeRejectedBeforeStaticValidation (
        PlanServiceResult result,
        RecordingRequestStaticValidationPreflightService staticPreflightService,
        RecordingRequestStaticValidationService staticValidationService)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Null(result.Output);
        Assert.Empty(staticPreflightService.Invocations);
        Assert.Empty(staticValidationService.Invocations);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("--readIndexMode", error.Message, StringComparison.Ordinal);
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteJsonInvocation PlanDispatched (
        RecordingUnityRequestExecutor requestExecutor)
    {
        return UnityRequestExecutorInvocationAssert.ExecuteJsonOnce(
            requestExecutor.Invocations,
            UcliCommandIds.Plan,
            UcliCommandIds.Plan);
    }

}
