using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Verify.VerifyFromInputReaderTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyFromInputReaderValidInputTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Read_WithValidCallInput_ReturnsNormalizedInput ()
    {
        const string command = "call";

        var result = VerifyFromInputReader.Read(
            CreateValidInputJson(
                CreateDefaultOpResultJson(
                    diagnosticsJson:
                    """
                    [
                      {
                        "code": "READ_SURFACE_PARTIAL",
                        "severity": "warning",
                        "coverageImpact": "partial",
                        "message": "Read surface coverage is partial."
                      }
                    ]
                    """),
                """
                {
                  "requirements": [
                    {
                      "surface": "sceneTreeLite",
                      "minSafeGeneratedAtUtc": "2026-05-17T00:00:00+00:00",
                      "scenePath": "Assets/Scenes/Main.unity"
                    }
                  ]
                }
                """,
                command),
            DefaultProjectFingerprint);

        Assert.True(result.IsSuccess);
        var input = result.Input!;
        Assert.Equal(command, input.Command);
        Assert.Equal(DefaultProjectFingerprint, input.ProjectFingerprint);
        Assert.Equal(1, input.ReadPostconditionRequirementCount);
        var opResult = Assert.Single(input.OpResults);
        Assert.Equal(0, opResult.Index);
        Assert.Equal("edit", opResult.Op);
        Assert.True(opResult.Applied);
        Assert.True(opResult.Changed);
        Assert.Equal(1, opResult.TouchedCount);
        Assert.Equal(IpcExecutePostReadSourceKind.Edit, opResult.PostReadSource.SourceKind);
        Assert.Equal(IpcExecutePostReadCommit.Context, opResult.PostReadSource.Commit);
        Assert.True(opResult.PostReadSource.PersistenceExpected);
        Assert.Equal(IpcExecuteExpectedPostState.Deterministic, opResult.PostReadSource.ExpectedPostState);
        var diagnostic = Assert.Single(opResult.Diagnostics);
        Assert.Equal("READ_SURFACE_PARTIAL", diagnostic.Code.Value);
        Assert.Equal(UcliDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(IpcExecuteDiagnosticCoverageImpact.Partial, diagnostic.CoverageImpact);
        Assert.Equal("Read surface coverage is partial.", diagnostic.Message);
        Assert.True(input.NeedsPostRead);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Read_WithNoOpInput_ReturnsNormalizedInput ()
    {
        var projectFingerprintText = DefaultProjectFingerprint.ToString();
        var result = VerifyFromInputReader.Read(
            $$"""
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "call",
              "payload": {
                "project": {
                  "projectFingerprint": "{{projectFingerprintText}}"
                },
                "opResults": [],
                "postReadSource": {
                  "schemaVersion": 1,
                  "steps": []
                }
              },
              "errors": []
            }
            """,
            DefaultProjectFingerprint);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Input!.OpResults);
        Assert.False(result.Input.NeedsPostRead);
    }
}
