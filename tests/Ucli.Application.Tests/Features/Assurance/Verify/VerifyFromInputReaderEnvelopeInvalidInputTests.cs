using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Verify.VerifyFromInputReaderTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyFromInputReaderEnvelopeInvalidInputTests
{
    public static TheoryData<string, UcliCode> InvalidEnvelopeCases
        => CreateInvalidInputTheoryData(EnumerateInvalidEnvelopeCases());

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidEnvelopeCases))]
    public void Read_WithInvalidEnvelope_ReturnsVerifyErrorCode (
        string json,
        UcliCode expectedCode)
    {
        var result = VerifyFromInputReader.Read(
            json,
            DefaultProjectFingerprint);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    private static IEnumerable<InvalidInputCase> EnumerateInvalidEnvelopeCases ()
    {
        var otherProjectFingerprintText = ProjectFingerprintTestFactory.Create("other-fingerprint").ToString();

        yield return new InvalidInputCase(
            "[]",
            VerifyErrorCodes.VerifyInputSchemaUnsupported
        );
        yield return new InvalidInputCase(
            """
            {
              "protocolVersion": 999,
              "command": "call",
              "payload": {}
            }
            """,
            VerifyErrorCodes.VerifyInputProtocolVersionMismatch
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputCommandUnsupported
        );
        yield return new InvalidInputCase(
            """
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "call",
              "errors": []
            }
            """,
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
                "opResults": []
              },
              "errors": []
            }
            """,
            VerifyErrorCodes.VerifyInputProjectMissing
        );
        yield return new InvalidInputCase(
            $$"""
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "call",
              "payload": {
                "project": {
                  "projectFingerprint": "{{otherProjectFingerprintText}}"
                },
                "opResults": []
              },
              "errors": []
            }
            """,
            VerifyErrorCodes.ProjectFingerprintMismatch
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
            """
            {
              "protocolVersion": 1,
              "status": "ok",
              "exitCode": 0,
              "command": "call",
              "payload": {}
            }
            """,
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
    }
}
