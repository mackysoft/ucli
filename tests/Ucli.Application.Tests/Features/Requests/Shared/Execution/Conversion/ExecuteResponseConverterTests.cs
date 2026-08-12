using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion.ExecuteResponseConverterTestSupport;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Execution;

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
            new UnityProjectIdentity(
                projectPath: Path.Combine(ExpectedProject.RepositoryRoot.Value, "AnotherUnityProject"),
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
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenProjectPathDoesNotMatch_ReturnsInternalErrorWithoutResponseData ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
            [],
            new UnityProjectIdentity(
                projectPath: Path.Combine(ExpectedProject.RepositoryRoot.Value, "AnotherUnityProject"),
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
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenUnityVersionDoesNotMatch_ReturnsInternalErrorWithoutResponseData ()
    {
        var response = CreateResponse(new IpcExecuteResponse(
            [],
            new UnityProjectIdentity(
                projectPath: ExpectedProject.UnityProjectRoot.Value,
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
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourcesAreMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithOperationResult(
            """
            {
              "op": "ucli.project.save",
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
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourceRequiredTextIsMissing_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithOperationResult(
            """
            {
              "op": "ucli.project.save",
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
                  "op": "ucli.project.save",
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
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenTouchedResourceKindIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithOperationResult(
            """
            {
              "op": "ucli.project.save",
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
