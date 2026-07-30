using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;

namespace MackySoft.Ucli.Application.Tests;

internal static class PlanServiceInvocationAssert
{
    public static RecordingRequestStaticValidator.Invocation AllowPlayModeUsedLiveStaticValidation (
        PlanServiceResult result,
        RecordingRequestStaticValidationPreflightService staticPreflightService,
        RecordingOperationCatalog operationCatalog,
        RecordingRequestStaticValidator staticValidator)
    {
        Assert.True(result.IsSuccess);
        Assert.Empty(staticPreflightService.Invocations);
        _ = Assert.Single(operationCatalog.ProjectGetAllInvocations);
        var validationInvocation = Assert.Single(staticValidator.Invocations);
        Assert.True(validationInvocation.Catalog.IsAvailable);
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
        RecordingOperationCatalog operationCatalog,
        RecordingRequestStaticValidator staticValidator)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Null(result.Output);
        Assert.Empty(staticPreflightService.Invocations);
        Assert.Empty(operationCatalog.ProjectGetAllInvocations);
        Assert.Empty(staticValidator.Invocations);
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
