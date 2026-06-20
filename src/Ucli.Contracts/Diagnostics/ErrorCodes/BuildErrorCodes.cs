namespace MackySoft.Ucli.Contracts;

/// <summary> Defines machine-readable error codes used by build assurance workflows. </summary>
public static class BuildErrorCodes
{
    /// <summary> Gets the error code emitted when a build profile cannot be parsed or validated. </summary>
    public static readonly UcliCode BuildProfileInvalid = new("BUILD_PROFILE_INVALID");

    /// <summary> Gets the error code emitted when a Unity Build Profile asset cannot be resolved or applied. </summary>
    public static readonly UcliCode BuildUnityBuildProfileInvalid = new("BUILD_UNITY_BUILD_PROFILE_INVALID");

    /// <summary> Gets the error code emitted when a build target stable name cannot be resolved. </summary>
    public static readonly UcliCode BuildTargetUnsupported = new("BUILD_TARGET_UNSUPPORTED");

    /// <summary> Gets the error code emitted when resolved build inputs cannot be converted to Unity BuildPipeline inputs. </summary>
    public static readonly UcliCode BuildInputsInvalid = new("BUILD_INPUTS_INVALID");

    /// <summary> Gets the error code emitted when a selected build scene is disabled. </summary>
    public static readonly UcliCode BuildSceneDisabled = new("BUILD_SCENE_DISABLED");

    /// <summary> Gets the error code emitted when the Unity installation does not support the requested build target. </summary>
    public static readonly UcliCode BuildTargetModuleMissing = new("BUILD_TARGET_MODULE_MISSING");

    /// <summary> Gets the error code emitted when resolved runtime policy rejects the selected execution or editor mode. </summary>
    public static readonly UcliCode BuildRuntimePolicyViolation = new("BUILD_RUNTIME_POLICY_VIOLATION");

    /// <summary> Gets the error code emitted when audited project items have unsaved changes. </summary>
    public static readonly UcliCode BuildDirtyStatePresent = new("BUILD_DIRTY_STATE_PRESENT");

    /// <summary> Gets the error code emitted when dirty-state coverage is not complete enough to run a build. </summary>
    public static readonly UcliCode BuildDirtyStateIndeterminate = new("BUILD_DIRTY_STATE_INDETERMINATE");

    /// <summary> Gets the error code emitted when Unity did not produce a BuildReport after BuildPipeline execution. </summary>
    public static readonly UcliCode BuildReportMissing = new("BUILD_REPORT_MISSING");

    /// <summary> Gets the error code emitted when build artifacts cannot be written. </summary>
    public static readonly UcliCode BuildArtifactWriteFailed = new("BUILD_ARTIFACT_WRITE_FAILED");

    /// <summary> Gets the error code emitted when the build output manifest cannot be generated. </summary>
    public static readonly UcliCode BuildOutputManifestFailed = new("BUILD_OUTPUT_MANIFEST_FAILED");

    /// <summary> Gets the error code emitted when the output manifest digest does not match persisted artifact content. </summary>
    public static readonly UcliCode BuildOutputDigestMismatch = new("BUILD_OUTPUT_DIGEST_MISMATCH");

    /// <summary> Gets the error code emitted when project mutation is detected while mutation is forbidden. </summary>
    public static readonly UcliCode BuildProjectMutationForbidden = new("BUILD_PROJECT_MUTATION_FORBIDDEN");

    /// <summary> Gets all build error codes owned by the build assurance workflow. </summary>
    public static IReadOnlyList<UcliCode> All { get; } =
    [
        BuildProfileInvalid,
        BuildUnityBuildProfileInvalid,
        BuildTargetUnsupported,
        BuildInputsInvalid,
        BuildSceneDisabled,
        BuildTargetModuleMissing,
        BuildRuntimePolicyViolation,
        BuildDirtyStatePresent,
        BuildDirtyStateIndeterminate,
        BuildReportMissing,
        BuildArtifactWriteFailed,
        BuildOutputManifestFailed,
        BuildOutputDigestMismatch,
        BuildProjectMutationForbidden,
    ];
}
