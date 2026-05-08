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
using MackySoft.Ucli.Application.Shared.Foundation;

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

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(DefaultApplicationFailureValues))]
    public void ApplicationFailure_Create_UsesDefaultCodeAndOutcome (
        int failureKind,
        string expectedCode,
        int expectedOutcome)
    {
        var kind = (ApplicationFailureKind)failureKind;
        var failure = ApplicationFailure.Create(kind, "Failure message.");

        Assert.Equal(kind, failure.Kind);
        Assert.Equal(expectedCode, failure.Code.Value);
        Assert.Equal((ApplicationOutcome)expectedOutcome, failure.Outcome);
        Assert.Equal("Failure message.", failure.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ApplicationFailure_FromCode_PreservesUnknownCodeAndOpId ()
    {
        var futureCode = new UcliErrorCode("FUTURE_TRANSPORT_FAILURE");

        var failure = ApplicationFailure.FromCode(futureCode, "Future transport failed.", "step-1");

        Assert.Equal(ApplicationFailureKind.ContractViolation, failure.Kind);
        Assert.Equal(ApplicationOutcome.ToolError, failure.Outcome);
        Assert.Equal(futureCode, failure.Code);
        Assert.Equal("Future transport failed.", failure.Message);
        Assert.Equal("step-1", failure.OpId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ApplicationFailure_WhenCodeIsMissing_Throws ()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ApplicationFailure(
            ApplicationFailureKind.InternalError,
            ApplicationOutcome.ToolError,
            default,
            "Failure message."));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ApplicationFailure_WhenKindIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationFailure.Create(
            (ApplicationFailureKind)999,
            "Failure message."));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ApplicationFailure_WhenMessageIsMissing_Throws ()
    {
        Assert.ThrowsAny<ArgumentException>(() => ApplicationFailure.InternalError(""));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ApplicationFailure_WhenOutcomeIsSuccess_Throws ()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ApplicationFailure(
            ApplicationFailureKind.InternalError,
            ApplicationOutcome.Success,
            UcliCoreErrorCodes.InternalError,
            "Failure message."));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenErrorsAreEmpty_Throws ()
    {
        var readIndex = CreateReadIndexInfo();
        var validateOutput = new ValidateExecutionOutput(readIndex);

        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("Plan failed.", []));
        Assert.ThrowsAny<ArgumentException>(() => CallServiceResult.Failure("Call failed.", []));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestId, [], [], "Query failed.", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ResolveServiceResultFactory.Failure(RequestId, [], [], readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ValidateServiceResult.ValidationFailure(validateOutput, "Static validation failed.", []));
        Assert.ThrowsAny<ArgumentException>(() => OperationExecuteResultFactory.Failure(RequestId, [], []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenFailureOutcomesAreMixed_ResolvesToolError ()
    {
        ApplicationFailure[] errors =
        [
            ApplicationFailure.InvalidInput("Invalid argument."),
            ApplicationFailure.ExternalProcessFailure(
                "Infrastructure failed.",
                outcome: ApplicationOutcome.InfrastructureError),
        ];

        Assert.Equal(ApplicationOutcome.ToolError, RequestServiceResultPolicy.ResolveFailureOutcome(errors));

        var result = PlanServiceResult.Failure("Plan failed.", errors);

        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenOnlyInvalidInputFailuresExist_ResolvesInvalidArgument ()
    {
        ApplicationFailure[] errors =
        [
            ApplicationFailure.InvalidInput("Invalid argument."),
            ApplicationFailure.ConfigurationError("Configuration is invalid."),
        ];

        var result = CallServiceResult.Failure("Call failed.", errors);

        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenOnlyInfrastructureFailuresExist_ResolvesInfrastructureError ()
    {
        ApplicationFailure[] errors =
        [
            ApplicationFailure.ExternalProcessFailure(
                "Unity test infrastructure failed.",
                outcome: ApplicationOutcome.InfrastructureError),
        ];

        var result = OperationExecuteResultFactory.Failure(RequestId, [], errors);

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenErrorCollectionContainsNull_Throws ()
    {
        var readIndex = CreateReadIndexInfo();
        ApplicationFailure[] errors =
        [
            null!,
        ];

        Assert.ThrowsAny<ArgumentException>(() => RequestServiceResultPolicy.ResolveFailureOutcome(errors));
        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("Plan failed.", errors));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestId, [], errors, "Query failed.", readIndex));
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

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenTopLevelMessageIsMissing_Throws ()
    {
        var readIndex = CreateReadIndexInfo();
        var errors = CreateErrors();
        var validateOutput = new ValidateExecutionOutput(readIndex);

        Assert.ThrowsAny<ArgumentException>(() => PlanServiceResult.Failure("", errors));
        Assert.ThrowsAny<ArgumentException>(() => CallServiceResult.Failure("", errors));
        Assert.ThrowsAny<ArgumentException>(() => QueryServiceResultFactory.Failure("query assets find", RequestId, [], errors, "", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => ResolveServiceResult.Failure(RequestId, [], errors, "", readIndex));
        Assert.ThrowsAny<ArgumentException>(() => OperationExecuteResult.Failure(RequestId, [], errors, ""));
        Assert.ThrowsAny<ArgumentException>(() => ValidateServiceResult.Failure("", UcliCoreErrorCodes.InternalError, validateOutput));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromExecutionErrorOverride_UsesFinalErrorCodeForOutcome ()
    {
        var result = PlanFailureResultFactory.FromExecutionError(
            ExecutionError.InternalError("Project path is invalid."),
            errorCode: UcliCoreErrorCodes.InvalidArgument);

        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ApplicationFailureKind.InvalidInput, error.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromExternalOperationError_NormalizesFallbackMessage ()
    {
        var queryResult = QueryServiceResultFactory.FromIpcError(
            "query assets find",
            RequestId,
            new OperationExecutionError(default, "", null),
            CreateReadIndexInfo());

        var queryError = Assert.Single(queryResult.Errors);
        Assert.Equal(ApplicationFailureKind.InternalError, queryError.Kind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, queryError.Code);
        Assert.Equal("uCLI query failed.", queryError.Message);
        Assert.Equal(ApplicationOutcome.ToolError, queryResult.Outcome);

        var resolveResult = ResolveServiceResultFactory.FromIpcError(
            RequestId,
            new OperationExecutionError(default, "", null),
            CreateReadIndexInfo());

        var resolveError = Assert.Single(resolveResult.Errors);
        Assert.Equal(ApplicationFailureKind.InternalError, resolveError.Kind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, resolveError.Code);
        Assert.Equal("uCLI resolve failed.", resolveError.Message);
        Assert.Equal("uCLI resolve failed.", resolveResult.Message);
        Assert.Equal(ApplicationOutcome.ToolError, resolveResult.Outcome);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidArgumentErrorCodeValues))]
    public void InvalidArgumentErrorCodes_MapToInvalidArgumentOutcome (UcliErrorCode errorCode)
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
    public void UnknownErrorCode_IsPreservedAndMapsToToolError ()
    {
        var futureErrorCode = new UcliErrorCode("FUTURE_TRANSPORT_FAILURE");
        var error = RequestServiceResultPolicy.FromTransportFailure(
            errorCode: futureErrorCode,
            message: "Future transport failed.");

        Assert.Equal(futureErrorCode, error.Code);
        Assert.Equal(ApplicationOutcome.ToolError, error.Outcome);
        Assert.Equal(ApplicationOutcome.ToolError, RequestServiceResultPolicy.ResolveOutcome(error.Code));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromTransportFailure_NormalizesBlankBoundaryMessage ()
    {
        var error = RequestServiceResultPolicy.FromTransportFailure(errorCode: default(UcliErrorCode), message: "");

        Assert.Equal(ApplicationFailureKind.InternalError, error.Kind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Equal("Request execution failed.", error.Message);
        Assert.Equal(ApplicationOutcome.ToolError, error.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromUnityRequestFailure_PreservesClassifiedOutcome ()
    {
        var failure = new UnityRequestFailure(
            PlanTokenErrorCodes.PlanTokenInvalid,
            "Plan token is invalid.",
            ApplicationOutcome.InvalidArgument);

        var requestFailure = RequestServiceResultPolicy.FromUnityRequestFailure(failure);

        Assert.Equal(ApplicationFailureKind.UnityIpcFailure, requestFailure.Error.Kind);
        Assert.Equal(PlanTokenErrorCodes.PlanTokenInvalid, requestFailure.Error.Code);
        Assert.Equal("Plan token is invalid.", requestFailure.Message);
        Assert.Equal(ApplicationOutcome.InvalidArgument, requestFailure.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityRequestFailure_WhenOutcomeDoesNotMatchCode_Throws ()
    {
        Assert.ThrowsAny<ArgumentException>(() => new UnityRequestFailure(
            UcliCoreErrorCodes.InvalidArgument,
            "Invalid argument.",
            ApplicationOutcome.ToolError));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_Errors_AreReturnedAsReadOnlySnapshot ()
    {
        var inputErrors = new List<ApplicationFailure>(CreateErrors());
        var result = PlanServiceResult.Failure(
            "Plan failed.",
            inputErrors);

        inputErrors[0] = ApplicationFailure.InvalidInput("Changed message.");

        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        var collection = Assert.IsAssignableFrom<ICollection<ApplicationFailure>>(result.Errors);
        Assert.True(collection.IsReadOnly);
    }

    public static TheoryData<int, string, int> DefaultApplicationFailureValues ()
    {
        return new TheoryData<int, string, int>
        {
            { (int)ApplicationFailureKind.InvalidInput, UcliCoreErrorCodes.InvalidArgument.Value, (int)ApplicationOutcome.InvalidArgument },
            { (int)ApplicationFailureKind.ConfigurationError, UcliCoreErrorCodes.InvalidArgument.Value, (int)ApplicationOutcome.InvalidArgument },
            { (int)ApplicationFailureKind.EnvironmentError, UcliCoreErrorCodes.InternalError.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.UnityIpcFailure, UcliCoreErrorCodes.InternalError.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.ExternalProcessFailure, UcliCoreErrorCodes.InternalError.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.ContractViolation, UcliCoreErrorCodes.InternalError.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.Timeout, ExecutionErrorCodes.IpcTimeout.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.Canceled, ExecutionErrorCodes.Canceled.Value, (int)ApplicationOutcome.ToolError },
            { (int)ApplicationFailureKind.InternalError, UcliCoreErrorCodes.InternalError.Value, (int)ApplicationOutcome.ToolError },
        };
    }

    public static TheoryData<UcliErrorCode> InvalidArgumentErrorCodeValues ()
    {
        return new TheoryData<UcliErrorCode>
        {
            UcliCoreErrorCodes.InvalidArgument,
            PlanTokenErrorCodes.PlanTokenRequired,
            PlanTokenErrorCodes.PlanTokenInvalid,
            PlanTokenErrorCodes.PlanTokenExpired,
            PlanTokenErrorCodes.PlanTokenRequestMismatch,
            PlanTokenErrorCodes.StateChangedSincePlan,
            ProjectContextErrorCodes.ProjectPathInvalidFormat,
            ProjectContextErrorCodes.ProjectPathNotFound,
            ProjectContextErrorCodes.UnityProjectMarkerMissing,
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

    private static IReadOnlyList<ApplicationFailure> CreateErrors ()
    {
        return
        [
            ApplicationFailure.InternalError("Failure message."),
        ];
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
