using System.Reflection;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Projection;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.Results;

public sealed class RequestServiceResultInvariantTests
{
    private const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    [Fact]
    [Trait("Size", "Small")]
    public void ResultTypes_DoNotExposeDirectConstructors ()
    {
        Type[] resultTypes =
        [
            typeof(PlanServiceResult),
            typeof(CallServiceResult),
            typeof(QueryServiceResult),
            typeof(ResolveServiceResult),
            typeof(ValidateServiceResult),
            typeof(OperationExecuteResult),
        ];

        for (var i = 0; i < resultTypes.Length; i++)
        {
            var constructors = resultTypes[i].GetConstructors(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);

            Assert.DoesNotContain(constructors, static constructor => !constructor.IsPrivate);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Success_WhenRequiredOutputIsMissing_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => PlanServiceResult.Success(null!, "uCLI plan completed."));
        Assert.Throws<ArgumentNullException>(() => CallServiceResult.Success(null!, "uCLI call completed."));
        Assert.Throws<ArgumentNullException>(() => ValidateServiceResult.Success(null!, "Static validation passed."));
        Assert.Throws<ArgumentNullException>(() => QueryServiceResultFactory.Success("query assets find", RequestId, [], null!));
        Assert.Throws<ArgumentNullException>(() => ResolveServiceResultFactory.Success(RequestId, [], null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenErrorsAreEmpty_Throws ()
    {
        var readIndex = CreateReadIndexInfo();
        var validateOutput = new ValidateExecutionOutput(readIndex);

        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("Plan failed.", [], ApplicationOutcome.ToolError));
        Assert.ThrowsAny<ArgumentException>(() => CallServiceResult.Failure("Call failed.", [], ApplicationOutcome.ToolError));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestId, [], [], ApplicationOutcome.ToolError, "Query failed.", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ResolveServiceResultFactory.Failure(RequestId, [], [], ApplicationOutcome.ToolError, readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ValidateServiceResult.ValidationFailure(validateOutput, "Static validation failed.", []));
        Assert.ThrowsAny<ArgumentException>(() => OperationExecuteResultFactory.Failure(RequestId, [], [], ApplicationOutcome.ToolError));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenOutcomeIsSuccess_Throws ()
    {
        var readIndex = CreateReadIndexInfo();
        var errors = CreateErrors();

        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("Plan failed.", errors, ApplicationOutcome.Success));
        Assert.ThrowsAny<ArgumentException>(() => CallServiceResult.Failure("Call failed.", errors, ApplicationOutcome.Success));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestId, [], errors, ApplicationOutcome.Success, "Query failed.", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ResolveServiceResultFactory.Failure(RequestId, [], errors, ApplicationOutcome.Success, readIndex));
        Assert.ThrowsAny<ArgumentException>(() => OperationExecuteResultFactory.Failure(RequestId, [], errors, ApplicationOutcome.Success));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenOutcomeDoesNotMatchErrorCodes_Throws ()
    {
        var readIndex = CreateReadIndexInfo();

        AssertOutcomeMismatchThrows((errors, outcome) => PlanServiceResult.Failure("Plan failed.", errors, outcome));
        AssertOutcomeMismatchThrows((errors, outcome) => CallServiceResult.Failure("Call failed.", errors, outcome));
        AssertOutcomeMismatchThrows((errors, outcome) => QueryServiceResultFactory.Failure("query assets find", RequestId, [], errors, outcome, "Query failed.", readIndex));
        AssertOutcomeMismatchThrows((errors, outcome) => ResolveServiceResultFactory.Failure(RequestId, [], errors, outcome, readIndex));
        AssertOutcomeMismatchThrows((errors, outcome) => OperationExecuteResultFactory.Failure(RequestId, [], errors, outcome));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenInvalidArgumentAndToolErrorsAreMixed_ResolvesToolError ()
    {
        var readIndex = CreateReadIndexInfo();
        OperationExecutionError[] errors =
        [
            new OperationExecutionError(IpcErrorCodes.InvalidArgument, "Invalid argument.", null),
            new OperationExecutionError(IpcErrorCodes.InternalError, "Internal error.", null),
        ];

        Assert.Equal(ApplicationOutcome.ToolError, RequestServiceResultPolicy.ResolveOutcome(errors));
        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("Plan failed.", errors, ApplicationOutcome.InvalidArgument));

        var result = QueryServiceResultFactory.Failure(
            "query assets find",
            RequestId,
            [],
            errors,
            ApplicationOutcome.ToolError,
            "Query failed.",
            readIndex);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenErrorCollectionContainsNull_Throws ()
    {
        var readIndex = CreateReadIndexInfo();
        OperationExecutionError[] errors =
        [
            null!,
        ];

        Assert.ThrowsAny<ArgumentException>(() => RequestServiceResultPolicy.ResolveOutcome(errors));
        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("Plan failed.", errors, ApplicationOutcome.ToolError));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestId, [], errors, ApplicationOutcome.ToolError, "Query failed.", readIndex));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenValidationErrorCollectionContainsNull_Throws ()
    {
        ValidationError[] validationErrors =
        [
            null!,
        ];

        Assert.ThrowsAny<ArgumentException>(() => RequestServiceResultPolicy.FromValidationErrors(validationErrors));
        Assert.ThrowsAny<ArgumentException>(() => OperationExecuteResultFactory.FromValidationErrors(RequestId, validationErrors));
        Assert.ThrowsAny<ArgumentException>(() => ValidateServiceResult.ValidationFailure(
            new ValidateExecutionOutput(CreateReadIndexInfo()),
            "Static validation failed.",
            validationErrors));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("", "Failure message.")]
    [InlineData("INTERNAL_ERROR", "")]
    public void Failure_WhenErrorCodeOrMessageIsMissing_Throws (
        string errorCode,
        string errorMessage)
    {
        var readIndex = CreateReadIndexInfo();
        OperationExecutionError[] errors =
        [
            new OperationExecutionError(errorCode, errorMessage, null),
        ];

        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("Plan failed.", errors, ApplicationOutcome.ToolError));
        Assert.ThrowsAny<ArgumentException>(() => CallServiceResult.Failure("Call failed.", errors, ApplicationOutcome.ToolError));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestId, [], errors, ApplicationOutcome.ToolError, "Query failed.", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ResolveServiceResultFactory.Failure(RequestId, [], errors, ApplicationOutcome.ToolError, readIndex));
        Assert.ThrowsAny<ArgumentException>(() => OperationExecuteResultFactory.Failure(RequestId, [], errors, ApplicationOutcome.ToolError));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenTopLevelMessageIsMissing_Throws ()
    {
        var readIndex = CreateReadIndexInfo();
        var errors = CreateErrors();
        var validateOutput = new ValidateExecutionOutput(readIndex);

        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("", errors, ApplicationOutcome.ToolError));
        Assert.ThrowsAny<ArgumentException>(() => CallServiceResult.Failure("", errors, ApplicationOutcome.ToolError));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestId, [], errors, ApplicationOutcome.ToolError, "", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ResolveServiceResult.Failure(RequestId, [], errors, ApplicationOutcome.ToolError, "", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => OperationExecuteResult.Failure(RequestId, [], errors, ApplicationOutcome.ToolError, ""));
        Assert.ThrowsAny<ArgumentException>(() => ValidateServiceResult.Failure("", IpcErrorCodes.InternalError, validateOutput));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromExecutionErrorOverride_UsesFinalErrorCodeForOutcome ()
    {
        var result = PlanFailureResultFactory.FromExecutionError(
            ExecutionError.InternalError("Project path is invalid."),
            errorCode: IpcErrorCodes.InvalidArgument);

        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InvalidArgument, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromExternalOperationError_NormalizesFallbackMessage ()
    {
        var queryResult = QueryServiceResultFactory.FromIpcError(
            "query assets find",
            RequestId,
            new OperationExecutionError("", "", null),
            CreateReadIndexInfo());

        var queryError = Assert.Single(queryResult.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, queryError.Code);
        Assert.Equal("uCLI query failed.", queryError.Message);
        Assert.Equal(ApplicationOutcome.ToolError, queryResult.Outcome);

        var resolveResult = ResolveServiceResultFactory.FromIpcError(
            RequestId,
            new OperationExecutionError("", "", null),
            CreateReadIndexInfo());

        var resolveError = Assert.Single(resolveResult.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, resolveError.Code);
        Assert.Equal("uCLI resolve failed.", resolveError.Message);
        Assert.Equal("uCLI resolve failed.", resolveResult.Message);
        Assert.Equal(ApplicationOutcome.ToolError, resolveResult.Outcome);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidArgumentErrorCodeValues))]
    public void InvalidArgumentErrorCodes_MapToInvalidArgumentOutcome (string errorCode)
    {
        var validationError = new ValidationError(errorCode, "Validation failed.", "step-1");

        Assert.Equal(ApplicationOutcome.InvalidArgument, RequestServiceResultPolicy.ResolveOutcome(errorCode));

        var operationResult = OperationExecuteResultFactory.FromValidationErrors(
            RequestId,
            [
                validationError,
            ]);
        Assert.Equal(ApplicationOutcome.InvalidArgument, operationResult.Outcome);
        Assert.Equal(errorCode, Assert.Single(operationResult.Errors).Code);

        var validateResult = ValidateServiceResult.ValidationFailure(
            new ValidateExecutionOutput(CreateReadIndexInfo()),
            "Static validation failed.",
            [
                validationError,
            ]);
        Assert.Equal(ApplicationOutcome.InvalidArgument, validateResult.Outcome);
        Assert.Equal(errorCode, Assert.Single(validateResult.Errors).Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromTransportFailure_NormalizesBlankBoundaryMessage ()
    {
        var error = RequestServiceResultPolicy.FromTransportFailure(errorCode: "", message: "");

        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Equal("Request execution failed.", error.Message);
        Assert.Equal(ApplicationOutcome.ToolError, RequestServiceResultPolicy.ResolveOutcome(error.Code));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_Errors_AreReturnedAsReadOnlySnapshot ()
    {
        var inputErrors = new List<OperationExecutionError>(CreateErrors());
        var result = PlanServiceResult.Failure(
            "Plan failed.",
            inputErrors,
            ApplicationOutcome.ToolError);

        inputErrors[0] = new OperationExecutionError(IpcErrorCodes.InvalidArgument, "Changed message.", null);

        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        var collection = Assert.IsAssignableFrom<ICollection<OperationExecutionError>>(result.Errors);
        Assert.True(collection.IsReadOnly);
    }

    private static IReadOnlyList<OperationExecutionError> CreateErrors ()
    {
        return
        [
            new OperationExecutionError(IpcErrorCodes.InternalError, "Failure message.", null),
        ];
    }

    public static TheoryData<string> InvalidArgumentErrorCodeValues ()
    {
        return new TheoryData<string>
        {
            IpcErrorCodes.InvalidArgument,
            IpcErrorCodes.PlanTokenRequired,
            IpcErrorCodes.PlanTokenInvalid,
            IpcErrorCodes.PlanTokenExpired,
            IpcErrorCodes.PlanTokenRequestMismatch,
            IpcErrorCodes.StateChangedSincePlan,
            ValidationErrorCodes.ProtocolVersionMismatch,
            ValidationErrorCodes.RequestIdInvalid,
            ValidationErrorCodes.StepsRequired,
            ValidationErrorCodes.StepIdRequired,
            ValidationErrorCodes.StepIdDuplicated,
            ValidationErrorCodes.StepKindRequired,
            ValidationErrorCodes.StepKindInvalid,
            ValidationErrorCodes.OperationNameRequired,
            ValidationErrorCodes.OperationNotFound,
            ValidationErrorCodes.OperationNotAllowed,
            ValidationErrorCodes.OperationArgsInvalid,
            ValidationErrorCodes.EditStepInvalid,
        };
    }

    private static void AssertOutcomeMismatchThrows (
        Action<IReadOnlyList<OperationExecutionError>, ApplicationOutcome> createFailure)
    {
        OperationExecutionError[] invalidArgumentErrors =
        [
            new OperationExecutionError(IpcErrorCodes.InvalidArgument, "Invalid argument.", null),
        ];
        OperationExecutionError[] internalErrors =
        [
            new OperationExecutionError(IpcErrorCodes.InternalError, "Internal error.", null),
        ];

        Assert.ThrowsAny<ArgumentException>(() => createFailure(invalidArgumentErrors, ApplicationOutcome.ToolError));
        Assert.ThrowsAny<ArgumentException>(() => createFailure(internalErrors, ApplicationOutcome.InvalidArgument));
    }

    private static ReadIndexInfo CreateReadIndexInfo ()
    {
        return new ReadIndexInfo(
            Used: true,
            Hit: true,
            Source: ReadIndexInfoSource.Index,
            Freshness: IndexFreshness.Fresh,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
            FallbackReason: null);
    }
}
