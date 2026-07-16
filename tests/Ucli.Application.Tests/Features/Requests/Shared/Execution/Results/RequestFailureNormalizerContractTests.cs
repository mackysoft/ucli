using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Tests.Execution.Results;

public sealed class RequestFailureNormalizerContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromTransportFailure_NormalizesBlankBoundaryMessage ()
    {
        var error = RequestFailureNormalizer.FromTransportFailure(errorCode: null, message: "");

        Assert.Equal(ApplicationFailureKind.InternalError, error.Kind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Equal("Request execution failed.", error.Message);
        Assert.Equal(ApplicationOutcome.ToolError, error.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromUnityRequestFailure_ReclassifiesInvalidArgumentCode ()
    {
        var failure = new UnityRequestFailure(
            UnityRequestFailureKind.General,
            PlanTokenErrorCodes.PlanTokenInvalid,
            "Plan token is invalid.");

        var requestFailure = RequestFailureNormalizer.FromUnityRequestFailure(failure);

        Assert.Equal(ApplicationFailureKind.InvalidInput, requestFailure.Kind);
        Assert.Equal(PlanTokenErrorCodes.PlanTokenInvalid, requestFailure.Code);
        Assert.Equal("Plan token is invalid.", requestFailure.Message);
        Assert.Equal(ApplicationOutcome.InvalidArgument, requestFailure.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityRequestFailure_WhenCodeOrMessageIsMissing_Throws ()
    {
        Assert.ThrowsAny<ArgumentException>(() => new UnityRequestFailure(
            UnityRequestFailureKind.General,
            null!,
            "Invalid argument."));
        Assert.ThrowsAny<ArgumentException>(() => new UnityRequestFailure(
            UnityRequestFailureKind.General,
            UcliCoreErrorCodes.InvalidArgument,
            ""));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityRequestFailure_WhenFailureKindIsInvalid_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UnityRequestFailure(
            (UnityRequestFailureKind)(-1),
            UcliCoreErrorCodes.InternalError,
            "Unity request failed."));
    }
}
