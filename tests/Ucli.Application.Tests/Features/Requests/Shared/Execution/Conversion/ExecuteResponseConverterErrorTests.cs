using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

using static MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion.ExecuteResponseConverterTestSupport;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

public sealed class ExecuteResponseConverterErrorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void UnityRequestResponse_WhenErrorsAreMissing_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new UnityRequestResponse(
            Payload: CreatePayload(),
            Errors: null!));

        Assert.Equal("Errors", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityRequestResponse_WhenPayloadIsUndefined_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new UnityRequestResponse(
            Payload: default,
            Errors: []));

        Assert.Equal("Payload", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityRequestResponse_WhenErrorsContainNull_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new UnityRequestResponse(
            Payload: CreatePayload(),
            Errors: [null!]));

        Assert.Equal("Errors", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationExecutionError_WhenCodeIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => new OperationExecutionError(
            null!,
            "Unity execution failed.",
            null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public void OperationExecutionError_WhenMessageIsMissing_ThrowsArgumentException (string? message)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new OperationExecutionError(
            UcliCoreErrorCodes.InternalError,
            message!,
            null));

        Assert.Equal("Message", exception.ParamName);
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
            ]);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlanTokenErrorCodes.PlanTokenInvalid, Assert.Single(result.Errors).Code);
    }

    private static JsonElement CreatePayload ()
    {
        return IpcPayloadCodec.SerializeToElement(CreateExecuteResponse([]));
    }
}
