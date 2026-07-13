using MackySoft.Ucli.Contracts.Ipc;

using static MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion.ExecuteResponseConverterTestSupport;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

public sealed class ExecuteResponseConverterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenExpectedProjectFingerprintIsNull_ThrowsArgumentNullException ()
    {
        var response = CreateResponse(CreateExecuteResponse([]));

        var exception = Assert.Throws<ArgumentNullException>(
            () => ExecuteResponseConverter.Convert(response, null!));

        Assert.Equal("expectedProjectFingerprint", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenProjectFingerprintMatches_Succeeds ()
    {
        var response = CreateResponse(CreateExecuteResponse([]));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExpectedProjectFingerprint, Assert.IsType<ProjectIdentityInfo>(result.Project).ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenProjectFingerprintDoesNotMatch_ReturnsInternalErrorWithoutResponseData ()
    {
        var responseProjectFingerprint = ProjectFingerprintTestFactory.Create("another-project");
        var response = CreateResponse(new IpcExecuteResponse(
            [],
            new IpcProjectIdentity(
                projectPath: "/repo/AnotherUnityProject",
                projectFingerprint: responseProjectFingerprint,
                unityVersion: "6000.1.4f1")));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.OpResults);
        Assert.Empty(result.ContractViolations);
        Assert.Null(result.Project);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("does not match the requested Unity project", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenProjectIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse("""
            {
              "project": null,
              "opResults": []
            }
            """);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("payload is invalid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenProjectPropertyIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse("""
            {
              "opResults": []
            }
            """);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("'project' field", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOpResultIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreateExecuteResponse(
        [
            null!,
        ]));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourcesAreMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreateExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: null!),
        ]));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].touched", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOpResultRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreateExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: null!,
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: []),
        ]));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].opId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourceRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreateExecuteResponse(
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

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].touched[0].kind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionRequirementsAreMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreateExecuteResponse([]) with
        {
            ReadPostcondition = new IpcExecuteReadPostcondition(null!),
        });

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("readPostcondition.requirements", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionSurfaceIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreateExecuteResponse([]) with
        {
            ReadPostcondition = new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: null!,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00")),
            ]),
        });

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("readPostcondition.requirements[0].surface", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOperationPhaseIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(CreateExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: "unknownPhase",
                Applied: true,
                Changed: true,
                Touched: []),
        ]));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].phase", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourceKindIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(CreateExecuteResponse(
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

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults[0].touched[0].kind", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionSurfaceIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(CreateExecuteResponse([]) with
        {
            ReadPostcondition = new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: "unknownSurface",
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00")),
            ]),
        });

        var result = ExecuteResponseConverter.Convert(response, ExpectedProjectFingerprint);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("readPostcondition.requirements[0].surface", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

}
