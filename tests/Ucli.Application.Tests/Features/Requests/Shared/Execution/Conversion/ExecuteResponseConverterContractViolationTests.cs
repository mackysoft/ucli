using MackySoft.Ucli.Contracts.Ipc;

using static MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion.ExecuteResponseConverterTestSupport;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

public sealed class ExecuteResponseConverterContractViolationTests
{
    private const string StepOne = "step-1";
    private const string StepTwo = "step-2";
    private const string ExpectedFact = "assurance.mayDirty=false";
    private const string ObservedResult = "opResults[].changed=true";
    private const string ContractViolationMessage = "Operation result violated declared assurance facts.";

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationsAreMissing_UsesEmptyCollection ()
    {
        var response = CreateResponse(new IpcExecuteResponse([]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.ContractViolations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationIsPresent_PropagatesViolation ()
    {
        var response = CreateContractViolationFailureResponse(CreateContractViolationPayload());

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var violation = Assert.Single(result.ContractViolations);
        Assert.Equal(StepOne, violation.OpId);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, violation.Operation);
        Assert.Equal(ExpectedFact, violation.ExpectedFact);
        Assert.Equal(ObservedResult, violation.ObservedResult);
        Assert.Equal(IpcExecuteApplicationStateNames.Indeterminate, violation.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationPayloadHasNoError_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse([])
        {
            ContractViolations =
            [
                CreateContractViolation(),
            ],
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("OPERATION_CONTRACT_VIOLATION", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationPayloadHasSuccessStatus_ReturnsInternalError ()
    {
        var response = CreateContractViolationFailureResponse(
            CreateContractViolationPayload(),
            hasFailureStatus: false);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("response status", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorOpIdDoesNotMatchPayload_ReturnsInternalError ()
    {
        var response = CreateContractViolationFailureResponse(
            CreateContractViolationPayload(),
            [CreateContractViolationError(StepTwo)]);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(StepOne, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorOpIdIsMissing_ReturnsInternalError ()
    {
        var response = CreateContractViolationFailureResponse(
            CreateContractViolationPayload(),
            [CreateContractViolationError(null)]);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("errors[0].opId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorHasNoMatchingPayloadItem_ReturnsInternalError ()
    {
        var response = CreateContractViolationFailureResponse(
            CreateContractViolationPayload(),
            [
                CreateContractViolationError(StepOne),
                CreateContractViolationError(StepTwo),
            ]);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(StepTwo, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationRequiredTextIsMissing_ReturnsInternalError ()
    {
        var projectFingerprintText = ProjectFingerprintTestFactory.Create("project-fingerprint").ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{projectFingerprintText}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "contractViolations": [
                {
                  "opId": "step-1",
                  "expectedFact": "assurance.mayDirty=false",
                  "observedResult": "opResults[].changed=true",
                  "applicationState": "indeterminate"
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("contractViolations[0].operation", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationApplicationStateIsUnsupported_ReturnsInternalError ()
    {
        var projectFingerprintText = ProjectFingerprintTestFactory.Create("project-fingerprint").ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "{{projectFingerprintText}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "contractViolations": [
                {
                  "opId": "step-1",
                  "operation": "ucli.project.refresh",
                  "expectedFact": "assurance.mayDirty=false",
                  "observedResult": "opResults[].changed=true",
                  "applicationState": "maybeApplied"
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("contractViolations[0].applicationState", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorHasNoPayloadItems_ReturnsInternalError ()
    {
        var response = CreateContractViolationFailureResponse(new IpcExecuteResponse([])
        {
            Project = CreateProjectIdentity(),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("'contractViolations' field", error.Message, StringComparison.Ordinal);
    }

    private static IpcExecuteResponse CreateContractViolationPayload ()
    {
        return new IpcExecuteResponse([])
        {
            Project = CreateProjectIdentity(),
            ContractViolations =
            [
                CreateContractViolation(),
            ],
        };
    }

    private static IpcExecuteContractViolation CreateContractViolation ()
    {
        return new IpcExecuteContractViolation(
            OpId: StepOne,
            Operation: UcliPrimitiveOperationNames.ProjectRefresh,
            ExpectedFact: ExpectedFact,
            ObservedResult: ObservedResult,
            ApplicationState: IpcExecuteApplicationStateNames.Indeterminate);
    }

    private static OperationExecutionError CreateContractViolationError (string? opId = StepOne)
    {
        return new OperationExecutionError(
            ExecuteRequestErrorCodes.OperationContractViolation,
            ContractViolationMessage,
            opId);
    }

    private static UnityRequestResponse CreateContractViolationFailureResponse (
        IpcExecuteResponse payload,
        IReadOnlyList<OperationExecutionError>? errors = null,
        bool hasFailureStatus = true)
    {
        return new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: errors ?? [CreateContractViolationError()],
            HasFailureStatus: hasFailureStatus);
    }
}
