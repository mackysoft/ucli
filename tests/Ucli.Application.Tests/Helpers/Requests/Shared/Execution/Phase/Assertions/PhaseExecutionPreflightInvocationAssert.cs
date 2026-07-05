using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

internal static class PhaseExecutionPreflightInvocationAssert
{
    public static void DeadlineExpiredBeforeCatalogLoad (
        PhaseExecutionPreflightResult result,
        PreparedRequestContext expectedPreparedRequest,
        RecordingOperationCatalog operationCatalog,
        RecordingRequestStaticValidator validationService)
    {
        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Contains("operation metadata discovery", result.Error.Message, StringComparison.Ordinal);
        Assert.NotNull(result.PreparedRequest);
        Assert.Same(expectedPreparedRequest, result.PreparedRequest!.PreparedRequest);
        Assert.Empty(result.PreparedRequest.OperationsByName);
        Assert.Empty(result.ValidationErrors);
        Assert.Empty(operationCatalog.ProjectGetAllInvocations);
        Assert.Empty(validationService.Invocations);
    }

    public static RecordingPhaseExecutionPreflightService.Invocation PreparedOnce (
        RecordingPhaseExecutionPreflightService preflightService,
        PhaseExecutionPreparedRequest expectedPreparedRequest,
        UnityExecutionMode expectedMode,
        bool expectedFailFast)
    {
        return PreparedOnce(
            preflightService,
            expectedPreparedRequest.PreparedRequest,
            expectedMode,
            expectedFailFast);
    }

    public static RecordingPhaseExecutionPreflightService.Invocation PreparedOnce (
        RecordingPhaseExecutionPreflightService preflightService,
        PreparedRequestContext expectedPreparedRequest,
        UnityExecutionMode expectedMode,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(preflightService.Invocations);
        Assert.Same(expectedPreparedRequest, invocation.PreparedRequest);
        Assert.Equal(expectedMode, invocation.Mode);
        Assert.Equal(expectedFailFast, invocation.FailFast);
        return invocation;
    }
}
