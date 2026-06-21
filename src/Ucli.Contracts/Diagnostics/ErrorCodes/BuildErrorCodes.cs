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

    /// <summary> Gets the error code emitted when an output source path violates build output path policy. </summary>
    public static readonly UcliCode BuildOutputPathInvalid = new("BUILD_OUTPUT_PATH_INVALID");

    /// <summary> Gets the error code emitted when <c>output-manifest.json.manifestDigest</c> does not match manifest content. </summary>
    public static readonly UcliCode BuildOutputManifestDigestMismatch = new("BUILD_OUTPUT_MANIFEST_DIGEST_MISMATCH");

    /// <summary> Gets the error code emitted when the output manifest artifact digest does not match file bytes. </summary>
    public static readonly UcliCode BuildOutputManifestArtifactDigestMismatch = new("BUILD_OUTPUT_MANIFEST_ARTIFACT_DIGEST_MISMATCH");

    /// <summary> Gets the error code emitted when project mutation is detected while mutation is forbidden. </summary>
    public static readonly UcliCode BuildProjectMutationForbidden = new("BUILD_PROJECT_MUTATION_FORBIDDEN");

    /// <summary> Gets the error code emitted when an executeMethod runner entrypoint cannot be resolved. </summary>
    public static readonly UcliCode BuildExecuteMethodNotFound = new("BUILD_EXECUTE_METHOD_NOT_FOUND");

    /// <summary> Gets the error code emitted when an executeMethod runner entrypoint is not static. </summary>
    public static readonly UcliCode BuildExecuteMethodNotStatic = new("BUILD_EXECUTE_METHOD_NOT_STATIC");

    /// <summary> Gets the error code emitted when an executeMethod runner entrypoint resolution is ambiguous. </summary>
    public static readonly UcliCode BuildExecuteMethodAmbiguous = new("BUILD_EXECUTE_METHOD_AMBIGUOUS");

    /// <summary> Gets the error code emitted when an executeMethod runner entrypoint has an unsupported signature. </summary>
    public static readonly UcliCode BuildExecuteMethodUnsupportedSignature = new("BUILD_EXECUTE_METHOD_UNSUPPORTED_SIGNATURE");

    /// <summary> Gets the error code emitted when a requested runner environment entry is missing. </summary>
    public static readonly UcliCode BuildRunnerEnvironmentMissing = new("BUILD_RUNNER_ENVIRONMENT_MISSING");

    /// <summary> Gets the error code emitted when runner invocation fails before a terminal result is observed. </summary>
    public static readonly UcliCode BuildRunnerInvocationFailed = new("BUILD_RUNNER_INVOCATION_FAILED");

    /// <summary> Gets the error code emitted when an executeMethod runner body throws before returning a valid result. </summary>
    public static readonly UcliCode BuildExecuteMethodInvocationFailed = new("BUILD_EXECUTE_METHOD_INVOCATION_FAILED");

    /// <summary> Gets the error code emitted when a required runner result cannot be obtained. </summary>
    public static readonly UcliCode BuildRunnerResultMissing = new("BUILD_RUNNER_RESULT_MISSING");

    /// <summary> Gets the error code emitted when a runner result does not satisfy the build runner contract. </summary>
    public static readonly UcliCode BuildRunnerResultInvalid = new("BUILD_RUNNER_RESULT_INVALID");

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
        BuildOutputPathInvalid,
        BuildOutputManifestDigestMismatch,
        BuildOutputManifestArtifactDigestMismatch,
        BuildProjectMutationForbidden,
        BuildExecuteMethodNotFound,
        BuildExecuteMethodNotStatic,
        BuildExecuteMethodAmbiguous,
        BuildExecuteMethodUnsupportedSignature,
        BuildRunnerEnvironmentMissing,
        BuildRunnerInvocationFailed,
        BuildExecuteMethodInvocationFailed,
        BuildRunnerResultMissing,
        BuildRunnerResultInvalid,
    ];
}
