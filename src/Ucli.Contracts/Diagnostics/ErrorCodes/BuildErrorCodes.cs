namespace MackySoft.Ucli.Contracts;

/// <summary> Defines machine-readable error codes used by build assurance workflows. </summary>
public static class BuildErrorCodes
{
    /// <summary> Gets the error code emitted when a build profile cannot be parsed or validated. </summary>
    public static readonly UcliCode BuildProfileInvalid = new("BUILD_PROFILE_INVALID");

    /// <summary> Gets the error code emitted when a build target stable name cannot be resolved. </summary>
    public static readonly UcliCode BuildTargetUnsupported = new("BUILD_TARGET_UNSUPPORTED");

    /// <summary> Gets the error code emitted when resolved build inputs cannot be converted to Unity BuildPipeline inputs. </summary>
    public static readonly UcliCode BuildInputsInvalid = new("BUILD_INPUTS_INVALID");

    /// <summary> Gets the error code emitted when the Unity installation does not support the requested build target. </summary>
    public static readonly UcliCode BuildTargetModuleMissing = new("BUILD_TARGET_MODULE_MISSING");

    /// <summary> Gets the error code emitted when build input scenes have unsaved changes. </summary>
    public static readonly UcliCode BuildDirtyStatePresent = new("BUILD_DIRTY_STATE_PRESENT");

    /// <summary> Gets all build error codes owned by the build assurance workflow. </summary>
    public static IReadOnlyList<UcliCode> All { get; } =
    [
        BuildProfileInvalid,
        BuildTargetUnsupported,
        BuildInputsInvalid,
        BuildTargetModuleMissing,
        BuildDirtyStatePresent,
    ];

}
