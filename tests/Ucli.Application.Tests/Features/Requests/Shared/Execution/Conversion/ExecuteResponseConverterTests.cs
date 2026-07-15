using MackySoft.Ucli.Contracts.Ipc;

using static MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion.ExecuteResponseConverterTestSupport;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

public sealed class ExecuteResponseConverterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenExpectedProjectIsNull_ThrowsArgumentNullException ()
    {
        var response = CreateResponse(CreateExecuteResponse([]));

        var exception = Assert.Throws<ArgumentNullException>(
            () => ExecuteResponseConverter.Convert(response, null!));

        Assert.Equal("expectedProject", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenProjectFingerprintMatches_Succeeds ()
    {
        var response = CreateResponse(CreateExecuteResponse([]));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectIdentityInfo.From(ExpectedProject), Assert.IsType<ProjectIdentityInfo>(result.Project));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenProjectFingerprintDoesNotMatch_ReturnsInternalErrorWithoutResponseData ()
    {
        var responseProjectFingerprint = ProjectFingerprintTestFactory.Create("another-project");
        var response = CreateResponse(new IpcExecuteResponse(
            [],
            new IpcProjectIdentity(
                projectPath: Path.Combine(ExpectedProject.RepositoryRoot, "AnotherUnityProject"),
                projectFingerprint: responseProjectFingerprint,
                unityVersion: "6000.1.4f1"),
            planToken: null,
            readPostcondition: null,
            postReadSource: null,
            contractViolations: null));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.OpResults);
        Assert.Empty(result.ContractViolations);
        Assert.Null(result.Project);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("project.projectFingerprint", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenProjectPathDoesNotMatch_ReturnsInternalErrorWithoutResponseData ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
            [],
            new IpcProjectIdentity(
                projectPath: Path.Combine(ExpectedProject.RepositoryRoot, "AnotherUnityProject"),
                projectFingerprint: ExpectedProject.ProjectFingerprint,
                unityVersion: ExpectedProject.UnityVersion),
            planToken: null,
            readPostcondition: null,
            postReadSource: null,
            contractViolations: null));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Project);
        var error = Assert.Single(result.Errors);
        Assert.Contains("project.projectPath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenUnityVersionDoesNotMatch_ReturnsInternalErrorWithoutResponseData ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
            [],
            new IpcProjectIdentity(
                projectPath: ExpectedProject.UnityProjectRoot,
                projectFingerprint: ExpectedProject.ProjectFingerprint,
                unityVersion: "different-version"),
            planToken: null,
            readPostcondition: null,
            postReadSource: null,
            contractViolations: null));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Project);
        var error = Assert.Single(result.Errors);
        Assert.Contains("project.unityVersion", error.Message, StringComparison.Ordinal);
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

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

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

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("'project' field", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOpResultIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithOperationResult("null"));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("opResults", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourcesAreMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithOperationResult(
            """
            {
              "opId": "refresh",
              "op": "ucli.project.refresh",
              "phase": "call",
              "applied": true,
              "changed": true,
              "touched": null,
              "diagnostics": []
            }
            """));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(IpcExecuteOperationResult.Touched), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOpResultRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithOperationResult(
            """
            {
              "opId": null,
              "op": "ucli.project.refresh",
              "phase": "call",
              "applied": true,
              "changed": true,
              "touched": [],
              "diagnostics": []
            }
            """));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(IpcExecuteOperationResult.OpId), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourceRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithOperationResult(
            """
            {
              "opId": "refresh",
              "op": "ucli.project.refresh",
              "phase": "call",
              "applied": true,
              "changed": true,
              "touched": [
                {
                  "path": "Assets/Example.txt",
                  "assetGuid": null
                }
              ],
              "diagnostics": []
            }
            """));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("Touched-resource kind must be specified.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionRequirementsAreMissing_ReturnsInternalError ()
    {
        var projectFingerprint = ExpectedProjectFingerprint.ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": {{ExpectedProjectPathJson}},
                "projectFingerprint": "{{projectFingerprint}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "readPostcondition": {
                "requirements": null
              }
            }
            """);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(IpcExecuteReadPostcondition.Requirements), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionSurfaceIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithReadPostconditionRequirement(
            """
            {
              "minSafeGeneratedAtUtc": "2026-04-25T00:00:00+00:00"
            }
            """));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("Read postcondition surface must be specified.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenOperationPhaseIsUnsupported_ReturnsInternalError ()
    {
        var projectFingerprint = ExpectedProjectFingerprint.ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": {{ExpectedProjectPathJson}},
                "projectFingerprint": "{{projectFingerprint}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {
                  "opId": "refresh",
                  "op": "ucli.project.refresh",
                  "phase": "unknownPhase",
                  "applied": true,
                  "changed": true,
                  "touched": [],
                  "diagnostics": []
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(IpcExecuteOperationPhase), error.Message, StringComparison.Ordinal);
        Assert.Contains("unknownPhase", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourceKindIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithOperationResult(
            """
            {
              "opId": "refresh",
              "op": "ucli.project.refresh",
              "phase": "call",
              "applied": true,
              "changed": true,
              "touched": [
                {
                  "kind": "unknownKind",
                  "path": "Assets/Example.txt",
                  "assetGuid": null
                }
              ],
              "diagnostics": []
            }
            """));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(UcliTouchedResourceKind), error.Message, StringComparison.Ordinal);
        Assert.Contains("unknownKind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenReadPostconditionSurfaceIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithReadPostconditionRequirement(
            """
            {
              "surface": "unknownSurface",
              "minSafeGeneratedAtUtc": "2026-04-25T00:00:00+00:00"
            }
            """));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(IpcExecuteReadPostconditionSurface), error.Message, StringComparison.Ordinal);
        Assert.Contains("unknownSurface", error.Message, StringComparison.Ordinal);
    }

    private static string CreatePayloadWithReadPostconditionRequirement (string requirementJson)
    {
        var projectFingerprint = ExpectedProjectFingerprint.ToString();
        return $$"""
            {
              "project": {
                "projectPath": {{ExpectedProjectPathJson}},
                "projectFingerprint": "{{projectFingerprint}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [],
              "readPostcondition": {
                "requirements": [
                  {{requirementJson}}
                ]
              }
            }
            """;
    }

    private static string CreatePayloadWithOperationResult (string operationResultJson)
    {
        var projectFingerprint = ExpectedProjectFingerprint.ToString();
        return $$"""
            {
              "project": {
                "projectPath": {{ExpectedProjectPathJson}},
                "projectFingerprint": "{{projectFingerprint}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {{operationResultJson}}
              ]
            }
            """;
    }

}
