using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

public sealed class ExecuteResponseConverterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOpResultIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            null!,
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourcesAreMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: null!),
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].touched", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOpResultRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: null!,
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: []),
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].opId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourceRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched:
                [
                    new IpcExecuteTouchedResource(
                        Kind: null!,
                        Path: "Assets/Example.txt",
                        Guid: null),
                ]),
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].touched[0].kind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionRequirementsAreMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse([])
        {
            ReadPostcondition = new IpcExecuteReadPostcondition(null!),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("readPostcondition.requirements", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionSurfaceIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse([])
        {
            ReadPostcondition = new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: null!,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00")),
            ]),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("readPostcondition.requirements[0].surface", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOperationPhaseIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: "unknownPhase",
                Applied: true,
                Changed: true,
                Touched: []),
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].phase", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourceKindIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched:
                [
                    new IpcExecuteTouchedResource(
                        Kind: "unknownKind",
                        Path: "Assets/Example.txt",
                        Guid: null),
                ]),
        ]));

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].touched[0].kind", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionSurfaceIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(new IpcExecuteResponse([])
        {
            ReadPostcondition = new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: "unknownSurface",
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00")),
            ]),
        });

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("readPostcondition.requirements[0].surface", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenErrorsAreMissing_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])),
            Errors: null!,
            HasFailureStatus: false);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("'errors' field", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenErrorRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])),
            Errors:
            [
                new OperationExecutionError(null!, "Unity execution failed.", null),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("errors[0].code", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenFailureStatusHasNoErrors_ReturnsStatusMessage ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])),
            Errors: [],
            HasFailureStatus: true,
            FailureStatus: "busy");

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Equal("Execute response failed with status 'busy'.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenPlanTokenValidationFails_ReturnsInvalidArgumentOutcome ()
    {
        var response = new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse([])),
            Errors:
            [
                new OperationExecutionError(IpcErrorCodes.PlanTokenInvalid, "Plan token is invalid.", null),
            ],
            HasFailureStatus: true);

        var result = ExecuteResponseConverter.Convert(response);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Equal(IpcErrorCodes.PlanTokenInvalid, Assert.Single(result.Errors).Code);
    }

    private static UnityRequestResponse CreateResponse (IpcExecuteResponse payload)
    {
        return new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: [],
            HasFailureStatus: false);
    }
}
