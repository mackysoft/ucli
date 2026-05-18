using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Catalog;

/// <summary> Provides descriptors for verify command input error codes. </summary>
internal static class VerifyErrorCodeDescriptors
{
    private static readonly IReadOnlyList<UcliCommand> AppliesToVerify = [UcliCommandIds.Verify];

    /// <summary> Gets verify error descriptors. </summary>
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        Create(
            VerifyErrorCodes.VerifyInputSchemaUnsupported,
            "The verify input schema is unsupported.",
            "The file passed to --from is not a supported public uCLI result JSON shape."),
        Create(
            VerifyErrorCodes.VerifyInputProtocolVersionMismatch,
            "The verify input protocol version is unsupported.",
            "The file passed to --from does not use the current public uCLI protocol version."),
        Create(
            VerifyErrorCodes.VerifyInputCommandUnsupported,
            "The verify input command is unsupported.",
            "The file passed to --from is not a supported mutation-result command for verify post-read claims."),
        Create(
            VerifyErrorCodes.VerifyInputPayloadInvalid,
            "The verify input payload is invalid.",
            "The file passed to --from is missing required payload fields or contains malformed post-read source data."),
        Create(
            VerifyErrorCodes.VerifyInputProjectMissing,
            "The verify input project identity is missing.",
            "The file passed to --from does not contain payload.project.projectFingerprint."),
        Create(
            VerifyErrorCodes.ProjectFingerprintMismatch,
            "The verify input belongs to another project fingerprint.",
            "The file passed to --from was produced for a different resolved Unity project fingerprint."),
    ];

    private static UcliErrorDescriptor Create (
        UcliCode code,
        string summary,
        string meaning)
    {
        return UcliErrorDescriptorFactory.Create(
            code: code,
            category: "verify",
            summary: summary,
            meaning: meaning,
            appliesTo: AppliesToVerify,
            possiblePhases: ["argumentParsing", "inputValidation"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].message", "payload.project"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Pass a supported uCLI mutation result JSON from the same project."),
            ],
            relatedCodes: [UcliCoreErrorCodes.InvalidArgument]);
    }
}
