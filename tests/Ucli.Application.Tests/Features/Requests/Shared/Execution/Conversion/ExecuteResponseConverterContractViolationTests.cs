using MackySoft.Ucli.Contracts.Ipc;

using static MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion.ExecuteResponseConverterTestSupport;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

public sealed class ExecuteResponseConverterContractViolationTests
{
    private const string FirstResultPath = "/opResults/0";
    private const string SecondResultPath = "/opResults/1";
    private const string ExpectedFact = "assurance.mayDirty=false";
    private const string ObservedResult = "opResults[].changed=true";
    private const string ContractViolationMessage = "Operation result violated declared assurance facts.";

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationsAreMissing_UsesEmptyCollection ()
    {
        var response = CreateResponse(CreateExecuteResponse([]));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.ContractViolations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationIsPresent_PropagatesViolation ()
    {
        var response = CreateContractViolationFailureResponse(CreateContractViolationPayload());

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var violation = Assert.Single(result.ContractViolations);
        Assert.Equal(FirstResultPath, violation.InstancePath);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, violation.Operation);
        Assert.Equal(ExpectedFact, violation.ExpectedFact);
        Assert.Equal(ObservedResult, violation.ObservedResult);
        Assert.Equal(IpcApplicationState.Indeterminate, violation.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationPayloadHasNoError_ReturnsInternalError ()
    {
        var response = CreateResponse(CreateExecuteResponse(
            [CreateOperationResult()],
            contractViolations:
            [
                CreateContractViolation(),
            ]));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("OPERATION_CONTRACT_VIOLATION", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorInstancePathDoesNotMatchPayload_ReturnsInternalError ()
    {
        var response = CreateContractViolationFailureResponse(
            CreateContractViolationPayload(),
            [CreateContractViolationError(SecondResultPath)]);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(FirstResultPath, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorInstancePathIsMissing_ReturnsInternalError ()
    {
        var response = CreateContractViolationFailureResponse(
            CreateContractViolationPayload(),
            [CreateContractViolationError(null)]);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("errors[0].instancePath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorHasNoMatchingPayloadItem_ReturnsInternalError ()
    {
        var response = CreateContractViolationFailureResponse(
            CreateContractViolationPayload(),
            [
                CreateContractViolationError(FirstResultPath),
                CreateContractViolationError(SecondResultPath),
            ]);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(SecondResultPath, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationRequiredTextIsMissing_ReturnsInternalError ()
    {
        var projectFingerprintText = ProjectFingerprintTestFactory.Create("project-fingerprint").ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": {{ExpectedProjectPathJson}},
                "projectFingerprint": "{{projectFingerprintText}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "contractViolations": [
                {
                  "instancePath": "/opResults/0",
                  "expectedFact": "assurance.mayDirty=false",
                  "observedResult": "opResults[].changed=true",
                  "applicationState": "indeterminate"
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(IpcExecuteContractViolation.Operation), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationApplicationStateIsUnsupported_ReturnsInternalError ()
    {
        var projectFingerprintText = ProjectFingerprintTestFactory.Create("project-fingerprint").ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": {{ExpectedProjectPathJson}},
                "projectFingerprint": "{{projectFingerprintText}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "contractViolations": [
                {
                  "instancePath": "/opResults/0",
                  "operation": "ucli.project.refresh",
                  "expectedFact": "assurance.mayDirty=false",
                  "observedResult": "opResults[].changed=true",
                  "applicationState": "maybeApplied"
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(IpcApplicationState), error.Message, StringComparison.Ordinal);
        Assert.Contains("maybeApplied", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenContractViolationErrorHasNoPayloadItems_ReturnsInternalError ()
    {
        var response = CreateContractViolationFailureResponse(CreateExecuteResponse([]));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("'contractViolations' field", error.Message, StringComparison.Ordinal);
    }

    private static IpcExecuteResponse CreateContractViolationPayload ()
    {
        return CreateExecuteResponse(
            [CreateOperationResult()],
            contractViolations:
            [
                CreateContractViolation(),
            ]);
    }

    private static IpcExecuteContractViolation CreateContractViolation ()
    {
        return new IpcExecuteContractViolation(
            InstancePath: FirstResultPath,
            Operation: UcliPrimitiveOperationNames.ProjectRefresh,
            ExpectedFact: ExpectedFact,
            ObservedResult: ObservedResult,
            ApplicationState: IpcApplicationState.Indeterminate);
    }

    private static IpcExecuteOperationResult CreateOperationResult ()
    {
        return new IpcExecuteOperationResult(
            Op: UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: IpcExecuteOperationPhase.Call,
            Applied: true,
            Changed: true,
            Touched: [],
            OperationDescriptorDigest: OperationDescriptorDigest,
            Verdict: null,
            Result: null,
            Diagnostics: []);
    }

    private static OperationExecutionError CreateContractViolationError (string? instancePath)
    {
        return new OperationExecutionError(
            ExecuteRequestErrorCodes.OperationContractViolation,
            ContractViolationMessage,
            instancePath);
    }

    private static UnityRequestResponse CreateContractViolationFailureResponse (
        IpcExecuteResponse payload,
        IReadOnlyList<OperationExecutionError>? errors = null)
    {
        return new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: errors ?? [CreateContractViolationError(FirstResultPath)]);
    }
}
