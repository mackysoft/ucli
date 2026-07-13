using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Verify.VerifyFromInputReaderTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyFromInputReaderOperationInvalidInputTests
{
    public static TheoryData<string, UcliCode> InvalidOperationCases
        => CreateInvalidInputTheoryData(EnumerateInvalidOperationCases());

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidOperationCases))]
    public void Read_WithInvalidOperationPayload_ReturnsVerifyErrorCode (
        string json,
        UcliCode expectedCode)
    {
        var result = VerifyFromInputReader.Read(
            json,
            DefaultProjectFingerprint);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    private static IEnumerable<InvalidInputCase> EnumerateInvalidOperationCases ()
    {
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
        yield return new InvalidInputCase(
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
            VerifyErrorCodes.VerifyInputPayloadInvalid
        );
    }
}
