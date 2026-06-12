namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines machine-readable error codes used by build assurance workflows. </summary>
internal static class BuildErrorCodes
{
    /// <summary> Gets the error code emitted when a build profile cannot be parsed or validated. </summary>
    public static readonly UcliCode BuildProfileInvalid = new("BUILD_PROFILE_INVALID");

    /// <summary> Gets the error code emitted when a build target stable name cannot be resolved. </summary>
    public static readonly UcliCode BuildTargetUnsupported = new("BUILD_TARGET_UNSUPPORTED");

    /// <summary> Gets the error code emitted when build artifacts cannot be persisted. </summary>
    public static readonly UcliCode BuildArtifactWriteFailed = new("BUILD_ARTIFACT_WRITE_FAILED");

    /// <summary> Gets the error code emitted when the build output manifest cannot be generated or persisted. </summary>
    public static readonly UcliCode BuildOutputManifestFailed = new("BUILD_OUTPUT_MANIFEST_FAILED");

    /// <summary> Gets the error code emitted when output digest accounting cannot be trusted. </summary>
    public static readonly UcliCode BuildOutputDigestMismatch = new("BUILD_OUTPUT_DIGEST_MISMATCH");

    /// <summary> Gets all build error codes owned by the build assurance workflow. </summary>
    public static IReadOnlyList<UcliCode> All { get; } =
    [
        BuildProfileInvalid,
        BuildTargetUnsupported,
        BuildArtifactWriteFailed,
        BuildOutputManifestFailed,
        BuildOutputDigestMismatch,
    ];

    /// <summary> Gets build error codes that represent caller-correctable invalid arguments. </summary>
    public static IReadOnlyList<UcliCode> InvalidArgumentCodes { get; } =
    [
        BuildProfileInvalid,
        BuildTargetUnsupported,
    ];
}
