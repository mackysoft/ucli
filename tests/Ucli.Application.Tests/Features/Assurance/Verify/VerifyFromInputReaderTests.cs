using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyFromInputReaderTests
{
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
              "command": "status",
              "payload": {}
            }
            """,
            VerifyErrorCodes.VerifyInputCommandUnsupported,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "command": "call"
            }
            """,
            VerifyErrorCodes.VerifyInputPayloadInvalid,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "command": "call",
              "payload": {
                "opResults": []
              }
            }
            """,
            VerifyErrorCodes.VerifyInputProjectMissing,
        ];
        yield return
        [
            """
            {
              "protocolVersion": 1,
              "command": "call",
              "payload": {
                "project": {
                  "projectFingerprint": "other-fingerprint"
                },
                "opResults": []
              }
            }
            """,
            VerifyErrorCodes.ProjectFingerprintMismatch,
        ];
    }
}
