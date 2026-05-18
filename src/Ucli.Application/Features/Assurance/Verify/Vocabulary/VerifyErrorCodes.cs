namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

/// <summary> Defines verify command input error codes. </summary>
internal static class VerifyErrorCodes
{
    /// <summary> Gets the error code emitted when a verify input schema is unsupported. </summary>
    public static readonly UcliCode VerifyInputSchemaUnsupported = new("VERIFY_INPUT_SCHEMA_UNSUPPORTED");

    /// <summary> Gets the error code emitted when a verify input protocol version is unsupported. </summary>
    public static readonly UcliCode VerifyInputProtocolVersionMismatch = new("VERIFY_INPUT_PROTOCOL_VERSION_MISMATCH");

    /// <summary> Gets the error code emitted when a verify input command is unsupported. </summary>
    public static readonly UcliCode VerifyInputCommandUnsupported = new("VERIFY_INPUT_COMMAND_UNSUPPORTED");

    /// <summary> Gets the error code emitted when a verify input payload is invalid. </summary>
    public static readonly UcliCode VerifyInputPayloadInvalid = new("VERIFY_INPUT_PAYLOAD_INVALID");

    /// <summary> Gets the error code emitted when a verify input project identity is missing. </summary>
    public static readonly UcliCode VerifyInputProjectMissing = new("VERIFY_INPUT_PROJECT_MISSING");

    /// <summary> Gets the error code emitted when a verify input belongs to another project fingerprint. </summary>
    public static readonly UcliCode ProjectFingerprintMismatch = new("PROJECT_FINGERPRINT_MISMATCH");

    /// <summary> Gets all verify input error codes. </summary>
    public static IReadOnlyList<UcliCode> All { get; } =
    [
        VerifyInputSchemaUnsupported,
        VerifyInputProtocolVersionMismatch,
        VerifyInputCommandUnsupported,
        VerifyInputPayloadInvalid,
        VerifyInputProjectMissing,
        ProjectFingerprintMismatch,
    ];
}
