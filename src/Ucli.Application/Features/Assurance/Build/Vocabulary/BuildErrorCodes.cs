namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines machine-readable error codes used by build assurance workflows. </summary>
internal static class BuildErrorCodes
{
    /// <summary> Gets the error code emitted when a build profile cannot be parsed or validated. </summary>
    public static readonly UcliCode BuildProfileInvalid = new("BUILD_PROFILE_INVALID");

    /// <summary> Gets the error code emitted when a build target stable name cannot be resolved. </summary>
    public static readonly UcliCode BuildTargetUnsupported = new("BUILD_TARGET_UNSUPPORTED");

    /// <summary> Gets all build error codes owned by the build assurance workflow. </summary>
    public static IReadOnlyList<UcliCode> All { get; } =
    [
        BuildProfileInvalid,
        BuildTargetUnsupported,
    ];
}
