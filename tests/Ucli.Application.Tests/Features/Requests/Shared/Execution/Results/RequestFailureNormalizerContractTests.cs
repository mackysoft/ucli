using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Tests.Execution.Results;

public sealed class RequestFailureNormalizerContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromExternalOperationError_NormalizesFallbackMessage ()
    {
        var queryResult = QueryServiceResultFactory.FromIpcError(
            "query assets find",
            RequestServiceResultInvariantTestSupport.RequestId,
            new OperationExecutionError(default, "", null),
            RequestServiceResultInvariantTestSupport.CreateReadIndexInfo());

        var queryError = Assert.Single(queryResult.Errors);
        Assert.Equal(ApplicationFailureKind.InternalError, queryError.Kind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, queryError.Code);
        Assert.Equal("uCLI query failed.", queryError.Message);
        Assert.Equal(ApplicationOutcome.ToolError, queryResult.Outcome);

        var resolveResult = ResolveServiceResultFactory.FromIpcError(
            RequestServiceResultInvariantTestSupport.RequestId,
            new OperationExecutionError(default, "", null),
            RequestServiceResultInvariantTestSupport.CreateReadIndexInfo());

        var resolveError = Assert.Single(resolveResult.Errors);
        Assert.Equal(ApplicationFailureKind.InternalError, resolveError.Kind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, resolveError.Code);
        Assert.Equal("uCLI resolve failed.", resolveError.Message);
        Assert.Equal("uCLI resolve failed.", resolveResult.Message);
        Assert.Equal(ApplicationOutcome.ToolError, resolveResult.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromTransportFailure_NormalizesBlankBoundaryMessage ()
    {
        var error = RequestFailureNormalizer.FromTransportFailure(errorCode: default(UcliCode), message: "");

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
            default,
            "Invalid argument."));
        Assert.ThrowsAny<ArgumentException>(() => new UnityRequestFailure(
            UcliCoreErrorCodes.InvalidArgument,
            ""));
    }
}
