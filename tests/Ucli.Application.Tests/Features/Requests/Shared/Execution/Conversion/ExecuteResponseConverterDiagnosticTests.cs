using MackySoft.Ucli.Contracts.Ipc;

using static MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion.ExecuteResponseConverterTestSupport;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

public sealed class ExecuteResponseConverterDiagnosticTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticsAreNull_ReturnsInternalError ()
    {
        var projectFingerprintText = ProjectFingerprintTestFactory.Create("project-fingerprint").ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": {{ExpectedProjectPathJson}},
                "projectFingerprint": "{{projectFingerprintText}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {
                  "opId": "refresh",
                  "op": "ucli.project.refresh",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [],
                  "diagnostics": null
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(IpcExecuteOperationResult.Diagnostics), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticsPropertyIsMissing_ReturnsInternalError ()
    {
        var projectFingerprintText = ProjectFingerprintTestFactory.Create("project-fingerprint").ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": {{ExpectedProjectPathJson}},
                "projectFingerprint": "{{projectFingerprintText}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {
                  "opId": "refresh",
                  "op": "ucli.project.refresh",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": []
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("diagnostics", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticSeverityIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithDiagnostic("unsupported", "partial"));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(UcliDiagnosticSeverity), error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticCoverageImpactIsMissing_ReturnsInternalError ()
    {
        var projectFingerprintText = ProjectFingerprintTestFactory.Create("project-fingerprint").ToString();
        var response = CreateResponse($$"""
            {
              "project": {
                "projectPath": {{ExpectedProjectPathJson}},
                "projectFingerprint": "{{projectFingerprintText}}",
                "unityVersion": "6000.1.4f1"
              },
              "opResults": [
                {
                  "opId": "query",
                  "op": "ucli.scene.query",
                  "phase": "plan",
                  "applied": false,
                  "changed": false,
                  "touched": [],
                  "diagnostics": [
                    {
                      "code": "HIERARCHY_PATH_UNREPRESENTABLE_OBJECTS",
                      "severity": "warning",
                      "message": "coverage is partial."
                    }
                  ]
                }
              ]
            }
            """);

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("Diagnostic coverage impact must be specified.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticCoverageImpactIsUnsupported_ReturnsInternalError ()
    {
        var response = CreateResponse(CreatePayloadWithDiagnostic("warning", "unsupported"));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains(nameof(IpcExecuteDiagnosticCoverageImpact), error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Convert_WhenDiagnosticIsPresent_PropagatesDiagnostic ()
    {
        var response = CreateResponse(CreateExecuteResponse(
        [
            new IpcExecuteOperationResult(
                OpId: new IpcExecuteStepId("query"),
                Op: UcliPrimitiveOperationNames.SceneQuery,
                Phase: IpcExecuteOperationPhase.Plan,
                Applied: false,
                Changed: false,
                Touched: [])
            {
                Diagnostics =
                [
                    new IpcExecuteDiagnostic(
                        ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                        UcliDiagnosticSeverity.Warning,
                        IpcExecuteDiagnosticCoverageImpact.Partial,
                        "Scene query skipped GameObjects whose names contain '/'."),
                ],
            },
        ]));

        var result = ExecuteResponseConverter.Convert(response, ExpectedProject);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Project);
        Assert.Equal(ExpectedProject.UnityProjectRoot.Value, result.Project.ProjectPath);
        Assert.Equal(ProjectFingerprintTestFactory.Create("project-fingerprint"), result.Project.ProjectFingerprint);
        Assert.Equal("6000.1.4f1", result.Project.UnityVersion);
        var opResult = Assert.Single(result.OpResults);
        var diagnostic = Assert.Single(opResult.Diagnostics);
        Assert.Equal(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects, diagnostic.Code);
        Assert.Equal(UcliDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(IpcExecuteDiagnosticCoverageImpact.Partial, diagnostic.CoverageImpact);
        Assert.Equal("Scene query skipped GameObjects whose names contain '/'.", diagnostic.Message);
    }

    private static string CreatePayloadWithDiagnostic (
        string severity,
        string coverageImpact)
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
                {
                  "opId": "refresh",
                  "op": "ucli.project.refresh",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [],
                  "diagnostics": [
                    {
                      "code": "HIERARCHY_PATH_UNREPRESENTABLE_OBJECTS",
                      "severity": "{{severity}}",
                      "coverageImpact": "{{coverageImpact}}",
                      "message": "coverage is partial."
                    }
                  ]
                }
              ]
            }
            """;
    }
}
