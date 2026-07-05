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
                      "minSafeGeneratedAtUtc": "2026-05-17T00:00:00+00:00"
                    }
                  ]
                }
                """,
                command),
            "project-fingerprint");

        Assert.True(result.IsSuccess);
        var input = result.Input!;
        Assert.Equal(command, input.Command);
        Assert.Equal("project-fingerprint", input.ProjectFingerprint);
        Assert.Equal(1, input.ReadPostconditionRequirementCount);
        var opResult = Assert.Single(input.OpResults);
        Assert.Equal("op-1", opResult.OpId);
        Assert.Equal("edit", opResult.Op);
        Assert.True(opResult.Applied);
        Assert.True(opResult.Changed);
        Assert.Equal(1, opResult.TouchedCount);
        Assert.Equal("edit", opResult.PostReadSource.SourceKind);
        Assert.Equal("context", opResult.PostReadSource.Commit);
        Assert.True(opResult.PostReadSource.PersistenceExpected);
        Assert.Equal("deterministic", opResult.PostReadSource.ExpectedPostState);
        var diagnostic = Assert.Single(opResult.Diagnostics);
        Assert.Equal("READ_SURFACE_PARTIAL", diagnostic.Code);
        Assert.Equal("warning", diagnostic.Severity);
        Assert.Equal("partial", diagnostic.CoverageImpact);
        Assert.Equal("Read surface coverage is partial.", diagnostic.Message);
        Assert.True(input.NeedsPostRead);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Read_WithValidRefreshInput_ReturnsNormalizedInput ()
    {
        var result = VerifyFromInputReader.Read(
            CreateValidInputJson(
                CreateDefaultOpResultJson(op: UcliPrimitiveOperationNames.ProjectRefresh),
                command: "refresh",
                postReadSourceJson:
                """
                {
                  "schemaVersion": 1,
                  "steps": [
                    {
                      "opId": "op-1",
                      "sourceKind": "refresh",
                      "playModeMutation": false,
                      "commit": null,
                      "persistenceExpected": true,
                      "expectedPostState": "unavailable"
                    }
                  ]
                }
                """),
            "project-fingerprint");

        Assert.True(result.IsSuccess);
        var input = result.Input!;
        Assert.Equal("refresh", input.Command);
        var opResult = Assert.Single(input.OpResults);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, opResult.Op);
        Assert.Equal("refresh", opResult.PostReadSource.SourceKind);
        Assert.Null(opResult.PostReadSource.Commit);
        Assert.True(opResult.PostReadSource.PersistenceExpected);
        Assert.Equal("unavailable", opResult.PostReadSource.ExpectedPostState);
        Assert.True(input.NeedsPostRead);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Read_WithNoOpInput_ReturnsNormalizedInput ()
    {
        var result = VerifyFromInputReader.Read(
            """
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "call",
              "payload": {
                "project": {
                  "projectFingerprint": "project-fingerprint"
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
            "project-fingerprint");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Input!.OpResults);
        Assert.False(result.Input.NeedsPostRead);
    }
}
