using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

using static MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion.ExecuteResponseConverterTestSupport;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

public sealed class ExecuteResponseConverterErrorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenErrorsAreMissing_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: CreatePayload(),
            Errors: null!,
            HasFailureStatus: false);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("'errors' field", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenErrorRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: CreatePayload(),
            Errors:
            [
                new OperationExecutionError(default, "Unity execution failed.", null),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("errors[0].code", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenFailureStatusHasNoErrors_ReturnsStatusMessage ()
    {
        var response = new UnityRequestResponse(
            Payload: CreatePayload(),
            Errors: [],
            HasFailureStatus: true,
            FailureStatus: "busy");

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Equal("Execute response failed with status 'busy'.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenPlanTokenValidationFails_PreservesOperationErrorCode ()
    {
        var response = new UnityRequestResponse(
            Payload: CreatePayload(),
            Errors:
            [
                new OperationExecutionError(PlanTokenErrorCodes.PlanTokenInvalid, "Plan token is invalid.", null),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlanTokenErrorCodes.PlanTokenInvalid, Assert.Single(result.Errors).Code);
    }

    private static JsonElement CreatePayload ()
    {
        return IpcPayloadCodec.SerializeToElement(CreateExecuteResponse([]));
    }
}
