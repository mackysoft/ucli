using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;

namespace MackySoft.Ucli.Application.Tests.Execution.Results;

public sealed class RequestServiceResultFailureInvariantTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenErrorsAreEmpty_Throws ()
    {
        var readIndex = RequestServiceResultInvariantTestSupport.CreateReadIndexInfo();
        var validateOutput = new ValidateExecutionOutput(ProjectIdentityInfoTestFactory.Create(), readIndex);

        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("Plan failed.", []));
        Assert.ThrowsAny<ArgumentException>(() => CallServiceResult.Failure("Call failed.", []));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestServiceResultInvariantTestSupport.RequestId, [], [], "Query failed.", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ResolveServiceResultFactory.Failure(RequestServiceResultInvariantTestSupport.RequestId, [], [], readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ValidateServiceResult.ValidationFailure(validateOutput, "Static validation failed.", []));
        Assert.ThrowsAny<ArgumentException>(() => OperationExecuteResultFactory.Failure(RequestServiceResultInvariantTestSupport.RequestId, [], []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenErrorCollectionContainsNull_Throws ()
    {
        var readIndex = RequestServiceResultInvariantTestSupport.CreateReadIndexInfo();
        ApplicationFailure[] errors =
        [
            null!,
        ];

        Assert.ThrowsAny<ArgumentException>(() => ApplicationFailureOutcomeResolver.Resolve(errors));
        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("Plan failed.", errors));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestServiceResultInvariantTestSupport.RequestId, [], errors, "Query failed.", readIndex));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenValidationErrorCollectionContainsNull_Throws ()
    {
        ValidationError[] validationErrors =
        [
            null!,
        ];

        Assert.ThrowsAny<ArgumentException>(() => RequestFailureNormalizer.FromValidationErrors(validationErrors));
        Assert.ThrowsAny<ArgumentException>(() => OperationExecuteResultFactory.FromValidationErrors(RequestServiceResultInvariantTestSupport.RequestId, validationErrors));
        Assert.ThrowsAny<ArgumentException>(() => ValidateServiceResult.ValidationFailure(
            new ValidateExecutionOutput(ProjectIdentityInfoTestFactory.Create(), RequestServiceResultInvariantTestSupport.CreateReadIndexInfo()),
            "Static validation failed.",
            validationErrors));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenTopLevelMessageIsMissing_Throws ()
    {
        var readIndex = RequestServiceResultInvariantTestSupport.CreateReadIndexInfo();
        var errors = RequestServiceResultInvariantTestSupport.CreateErrors();
        var validateOutput = new ValidateExecutionOutput(ProjectIdentityInfoTestFactory.Create(), readIndex);

        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("", errors));
        Assert.ThrowsAny<ArgumentException>(() => CallServiceResult.Failure("", errors));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestServiceResultInvariantTestSupport.RequestId, [], errors, "", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ResolveServiceResult.Failure(RequestServiceResultInvariantTestSupport.RequestId, [], errors, "", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => OperationExecuteResult.Failure(RequestServiceResultInvariantTestSupport.RequestId, [], errors, ""));
        Assert.ThrowsAny<ArgumentException>(() => ValidateServiceResult.Failure("", UcliCoreErrorCodes.InternalError, validateOutput));
    }
}
