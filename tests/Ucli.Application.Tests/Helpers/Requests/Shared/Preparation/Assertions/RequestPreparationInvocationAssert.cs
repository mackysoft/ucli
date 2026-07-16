using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

internal static class RequestPreparationInvocationAssert
{
    public static void NormalizationFailureReturnedBeforeParsing (
        RequestPreparationResult result,
        ExecutionError expectedError,
        RecordingValidateRequestJsonParser parser)
    {
        Assert.False(result.IsSuccess);
        Assert.Same(expectedError, result.Error);
        Assert.Empty(parser.Invocations);
        Assert.Null(result.PreparedRequest);
    }

    public static RecordingValidateRequestJsonParser.Invocation RequestJsonParsedOnce (
        RecordingValidateRequestJsonParser parser,
        string expectedRequestJson)
    {
        var invocation = Assert.Single(parser.Invocations);
        Assert.Equal(expectedRequestJson, invocation.RequestJson);
        return invocation;
    }

    public static RecordingRequestPreparationService.PrepareInvocation ProjectPreparedOnce (
        RecordingRequestPreparationService requestPreparationService,
        string? expectedProjectPath,
        string expectedRequestJson)
    {
        var invocation = Assert.Single(requestPreparationService.PrepareInvocations);
        Assert.Equal(expectedProjectPath, invocation.ProjectPath);
        Assert.Equal(expectedRequestJson, invocation.RequestJson);
        return invocation;
    }
}
