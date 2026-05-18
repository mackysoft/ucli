using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyFromInputReaderTests
{
    [Theory]
    [InlineData("call")]
    [InlineData("refresh")]
    [Trait("Size", "Small")]
    public void Read_WithValidInput_ReturnsNormalizedInput (string command)
    {
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
        Assert.Equal("Scene.Touch", opResult.Op);
        Assert.True(opResult.Applied);
        Assert.True(opResult.Changed);
        Assert.Equal(1, opResult.TouchedCount);
        var diagnostic = Assert.Single(opResult.Diagnostics);
        Assert.Equal("READ_SURFACE_PARTIAL", diagnostic.Code);
        Assert.Equal("warning", diagnostic.Severity);
        Assert.Equal("partial", diagnostic.CoverageImpact);
        Assert.Equal("Read surface coverage is partial.", diagnostic.Message);
        Assert.True(input.NeedsPostRead);
    }

    [Theory]
    [MemberData(nameof(GetInvalidInputCases))]
    [Trait("Size", "Small")]
    public void Read_WithInvalidInput_ReturnsVerifyErrorCode (
        string json,
        UcliCode expectedCode)
    {
        var result = VerifyFromInputReader.Read(
            json,
            "project-fingerprint");

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    public static IEnumerable<object[]> GetInvalidInputCases ()
    {
        yield return
        [
            "[]",
            VerifyErrorCodes.VerifyInputSchemaUnsupported,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 999,
              "command": "call",
              "payload": {}
            }
            """,
            VerifyErrorCodes.VerifyInputProtocolVersionMismatch,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "status",
              "payload": {},
              "errors": []
            }
            """,
            VerifyErrorCodes.VerifyInputCommandUnsupported,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "call",
              "errors": []
            }
            """,
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "call",
              "payload": {
                "opResults": []
              },
              "errors": []
            }
            """,
            VerifyErrorCodes.VerifyInputProjectMissing,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "call",
              "payload": {
                "project": {
                  "projectFingerprint": "other-fingerprint"
                },
                "opResults": []
              },
              "errors": []
            }
            """,
            VerifyErrorCodes.ProjectFingerprintMismatch,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "status": "error",
              "exitCode": 3,
              "command": "call",
              "payload": {},
              "errors": [
                {
                  "code": "INVALID_ARGUMENT",
                  "message": "Invalid input."
                }
              ]
            }
            """,
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 1,
              "command": "call",
              "payload": {},
              "errors": []
            }
            """,
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "call",
              "payload": {},
              "errors": [
                {
                  "code": "INVALID_ARGUMENT",
                  "message": "Invalid input."
                }
              ]
            }
            """,
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "status": "error",
              "exitCode": 0,
              "command": "call",
              "payload": {},
              "errors": []
            }
            """,
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "call",
              "payload": {}
            }
            """,
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            CreateValidInputJson("""
                {
                  "opId": "op-1",
                  "op": "Scene.Touch",
                  "phase": "unknown",
                  "applied": true,
                  "changed": true,
                  "touched": [
                    {
                      "kind": "asset",
                      "path": "Assets/Scene.unity"
                    }
                  ],
                  "diagnostics": []
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            CreateValidInputJson("""
                {
                  "opId": "op-1",
                  "op": "Scene.Touch",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [
                    {
                      "kind": "asset",
                      "path": "Assets/Scene.unity"
                    }
                  ],
                  "diagnostics": [
                    {
                      "code": "READ_SURFACE_UNKNOWN",
                      "severity": "unknown",
                      "coverageImpact": "none",
                      "message": "Unknown severity."
                    }
                  ]
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            CreateValidInputJson("""
                {
                  "opId": "op-1",
                  "op": "Scene.Touch",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [
                    {
                      "kind": "asset",
                      "path": "Assets/Scene.unity"
                    }
                  ],
                  "diagnostics": [
                    {
                      "code": "read_surface_unknown",
                      "severity": "warning",
                      "coverageImpact": "none",
                      "message": "Invalid diagnostic code."
                    }
                  ]
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            CreateValidInputJson("""
                {
                  "opId": "op-1",
                  "op": "Scene.Touch",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [
                    {
                      "kind": "asset",
                      "path": "Assets/Scene.unity"
                    }
                  ],
                  "diagnostics": [
                    {
                      "code": "READ_SURFACE_UNKNOWN",
                      "severity": "warning",
                      "coverageImpact": "unknown",
                      "message": "Unknown coverage."
                    }
                  ]
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            CreateValidInputJson("""
                {
                  "opId": "op-1",
                  "op": "Scene.Touch",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [
                    {
                      "kind": "unknown",
                      "path": "Assets/Scene.unity"
                    }
                  ],
                  "diagnostics": []
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            CreateValidInputJson("""
                {
                  "opId": "op-1",
                  "op": "Scene.Touch",
                  "phase": "call",
                  "applied": true,
                  "changed": true,
                  "touched": [
                    {
                      "kind": "asset",
                      "path": " "
                    }
                  ],
                  "diagnostics": []
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            CreateValidInputJson(CreateDefaultOpResultJson(), """
            {
            }
            """),
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            CreateValidInputJson(CreateDefaultOpResultJson(), """
            {
              "requirements": [
                {
                  "surface": "unknown"
                }
              ]
            }
            """),
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            CreateValidInputJson(CreateDefaultOpResultJson(), """
            {
              "requirements": [
                {
                  "surface": "sceneTreeLite"
                }
              ]
            }
            """),
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            CreateValidInputJson(CreateDefaultOpResultJson(), """
            {
              "requirements": [
                {
                  "surface": "sceneTreeLite",
                  "minSafeGeneratedAtUtc": "not-a-timestamp"
                }
              ]
            }
            """),
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
    }

    private static string CreateDefaultOpResultJson (string diagnosticsJson = "[]")
    {
        return $$"""
        {
          "opId": "op-1",
          "op": "Scene.Touch",
          "phase": "call",
          "applied": true,
          "changed": true,
          "touched": [
            {
              "kind": "asset",
              "path": "Assets/Scene.unity"
            }
          ],
          "diagnostics": {{diagnosticsJson}}
        }
        """;
    }

    private static string CreateValidInputJson (
        string opResultJson,
        string? readPostconditionJson = null,
        string command = "call")
    {
        var readPostconditionProperty = string.IsNullOrWhiteSpace(readPostconditionJson)
            ? string.Empty
            : $"""
              ,
              "readPostcondition": {readPostconditionJson}
            """;
        return $$"""
        {
          "protocolVersion": 1,
          "status": "ok",
          "exitCode": 0,
          "command": "{{command}}",
          "payload": {
            "project": {
              "projectFingerprint": "project-fingerprint"
            },
            "opResults": [
              {{opResultJson}}
            ]{{readPostconditionProperty}}
          },
          "errors": []
        }
        """;
    }
}
