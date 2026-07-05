using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;

namespace MackySoft.Ucli.Application.Tests;

internal static class ValidateServiceAssert
{
    public static void PreparationFailureStoppedBeforeSharedPreflight (
        ValidateServiceResult result,
        RecordingRequestStaticValidationPreflightService preflightService,
        UcliCode expectedErrorCode)
    {
        var error = AssertFailureWithoutOutput(result);
        Assert.Equal(expectedErrorCode, error.Code);
        Assert.Empty(preflightService.Invocations);
    }

    public static void InvalidTimeoutStoppedBeforeSharedPreflight (
        ValidateServiceResult result,
        RecordingRequestStaticValidationPreflightService preflightService,
        string expectedMessageFragment)
    {
        var error = AssertFailureWithoutOutput(result);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains(expectedMessageFragment, error.Message, StringComparison.Ordinal);
        Assert.Empty(preflightService.Invocations);
    }

    public static void SharedPreflightFailureReturnedWithReadIndexOutput (
        ValidateServiceResult result,
        RecordingRequestStaticValidationPreflightService preflightService,
        UcliCode expectedErrorCode,
        bool expectedReadIndexUsed)
    {
        var error = AssertFailureWithOutput(result);
        Assert.Equal(expectedErrorCode, error.Code);
        Assert.Equal(expectedReadIndexUsed, result.Output!.ReadIndex.Used);
        Assert.Single(preflightService.Invocations);
    }

    public static void SharedPreflightTimedOutAfterPrepareAttempt (
        ValidateServiceResult result,
        RecordingRequestStaticValidationPreflightService preflightService)
    {
        var error = AssertFailureWithoutOutput(result);
        Assert.Equal(ApplicationFailureKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Single(preflightService.Invocations);
    }

    public static void ReadIndexDisabledValidationTimedOutWithoutSharedPreflight (
        ValidateServiceResult result,
        RecordingRequestStaticValidationPreflightService preflightService)
    {
        var error = AssertFailureWithOutput(result);
        Assert.Equal(ApplicationFailureKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        AssertReadIndexDisabled(result.Output!);
        Assert.Empty(preflightService.Invocations);
    }

    public static void SharedPreflightSuccessReturned (
        ValidateServiceResult result,
        RecordingRequestStaticValidationPreflightService preflightService,
        bool expectedReadIndexUsed)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal("Static validation passed.", result.Message);
        Assert.NotNull(result.Output);
        Assert.Equal(expectedReadIndexUsed, result.Output!.ReadIndex.Used);
        Assert.Empty(result.Errors);
        Assert.Single(preflightService.Invocations);
    }

    public static ValidateExecutionOutput ReadIndexDisabledSuccessReturnedWithoutSharedPreflight (
        ValidateServiceResult result,
        RecordingRequestStaticValidationPreflightService preflightService)
    {
        Assert.True(result.IsSuccess);
        var output = Assert.IsType<ValidateExecutionOutput>(result.Output);
        AssertReadIndexDisabled(output);
        Assert.Empty(result.Errors);
        Assert.Empty(preflightService.Invocations);
        return output;
    }

    private static ApplicationFailure AssertFailureWithoutOutput (ValidateServiceResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        return Assert.Single(result.Errors);
    }

    private static ApplicationFailure AssertFailureWithOutput (ValidateServiceResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Output);
        return Assert.Single(result.Errors);
    }

    private static void AssertReadIndexDisabled (ValidateExecutionOutput output)
    {
        Assert.False(output.ReadIndex.Used);
        Assert.False(output.ReadIndex.Hit);
        Assert.Equal("readIndex disabled by mode.", output.ReadIndex.FallbackReason);
    }
}
