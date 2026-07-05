using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Verify.VerifyFromInputReaderTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyFromInputReaderPostReadInvalidInputTests
{
    public static TheoryData<string, UcliCode> InvalidPostReadCases
        => CreateInvalidInputTheoryData(EnumerateInvalidPostReadCases());

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidPostReadCases))]
    public void Read_WithInvalidPostReadPayload_ReturnsVerifyErrorCode (
        string json,
        UcliCode expectedCode)
    {
        var result = VerifyFromInputReader.Read(
            json,
            ProjectFingerprint);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    private static IEnumerable<InvalidInputCase> EnumerateInvalidPostReadCases ()
    {
        yield return new InvalidInputCase(
            CreateValidInputJson(CreateDefaultOpResultJson(), """
            {
            }
            """),
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
            CreateValidInputJson(CreateDefaultOpResultJson(), """
            {
              "requirements": [
                {
                  "surface": "unknown"
                }
              ]
            }
            """),
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
            CreateValidInputJson(CreateDefaultOpResultJson(), """
            {
              "requirements": [
                {
                  "surface": "sceneTreeLite"
                }
              ]
            }
            """),
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
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
                "opResults": [
                  {
                    "opId": "op-1",
                    "op": "Scene.Touch",
                    "phase": "call",
                    "applied": true,
                    "changed": true,
                    "touched": [],
                    "diagnostics": []
                  }
                ]
              },
              "errors": []
            }
            """,
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
            CreateValidInputJson(
                CreateDefaultOpResultJson(),
                postReadSourceJson:
                """
                {
                  "schemaVersion": 1,
                  "steps": [
                    {
                      "opId": "op-1",
                      "sourceKind": "edit",
                      "playModeMutation": false,
                      "commit": "invalid",
                      "persistenceExpected": true,
                      "expectedPostState": "deterministic"
                    }
                  ]
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
            CreateValidInputJson(
                CreateDefaultOpResultJson(op: UcliPrimitiveOperationNames.SceneOpen),
                postReadSourceJson:
                """
                {
                  "schemaVersion": 1,
                  "steps": [
                    {
                      "opId": "op-1",
                      "sourceKind": "operation",
                      "playModeMutation": false,
                      "commit": null,
                      "persistenceExpected": false,
                      "expectedPostState": "deterministic"
                    }
                  ]
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
            CreateValidInputJson(
                CreateDefaultOpResultJson(),
                postReadSourceJson:
                """
                {
                  "schemaVersion": 1,
                  "steps": [
                    {
                      "opId": "op-1",
                      "sourceKind": "edit",
                      "playModeMutation": false,
                      "commit": "context",
                      "persistenceExpected": false,
                      "expectedPostState": "deterministic"
                    }
                  ]
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
            CreateValidInputJson(
                CreateDefaultOpResultJson(op: UcliPrimitiveOperationNames.SceneOpen),
                postReadSourceJson:
                """
                {
                  "schemaVersion": 1,
                  "steps": [
                    {
                      "opId": "op-1",
                      "sourceKind": "edit",
                      "playModeMutation": false,
                      "commit": "context",
                      "persistenceExpected": true,
                      "expectedPostState": "deterministic"
                    }
                  ]
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
            CreateValidInputJson(
                CreateDefaultOpResultJson(),
                postReadSourceJson:
                """
                {
                  "schemaVersion": 1,
                  "steps": [
                    {
                      "opId": "op-1",
                      "sourceKind": "unknown",
                      "playModeMutation": false,
                      "commit": null,
                      "persistenceExpected": false,
                      "expectedPostState": "unavailable"
                    }
                  ]
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
            CreateValidInputJson(
                CreateDefaultOpResultJson(),
                postReadSourceJson:
                """
                {
                  "schemaVersion": 1,
                  "steps": [
                    {
                      "opId": "other-op",
                      "sourceKind": "operation",
                      "playModeMutation": false,
                      "commit": null,
                      "persistenceExpected": false,
                      "expectedPostState": "unavailable"
                    }
                  ]
                }
                """),
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
    }
}
