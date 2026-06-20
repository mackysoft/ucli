namespace MackySoft.Ucli.Contracts;

/// <summary> Provides descriptors for build command error codes. </summary>
internal static class BuildErrorCodeDescriptors
{
    private static readonly IReadOnlyList<UcliCommand> AppliesToBuildRun = [UcliCommandIds.BuildRun];

    /// <summary> Gets build error descriptors. </summary>
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildProfileInvalid,
            category: "build",
            summary: "The build profile is invalid.",
            meaning: "The build profile JSON could not be parsed, is missing required properties, or contains values outside the build profile contract.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["inputValidation", "profileResolution"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].message", "payload.build.profile"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Fix the build profile JSON so it matches the current build profile schema."),
            ],
            relatedCodes: [UcliCoreErrorCodes.InvalidArgument]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildTargetUnsupported,
            category: "build",
            summary: "The build target is unsupported.",
            meaning: "The build profile target stable name is not one of the uCLI build target names supported by this version.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["profileResolution"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].message", "payload.build.target"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Use a supported build target stable name in the build profile."),
            ],
            relatedCodes: [BuildErrorCodes.BuildProfileInvalid]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildInputsInvalid,
            category: "build",
            summary: "The resolved build inputs are invalid.",
            meaning: "The resolved build profile produced inputs that cannot be converted into a valid Unity BuildPipeline invocation.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["preconditionProbe", "buildInputResolution"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].message", "payload.build.input"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Fix the resolved build target or scene input so Unity can construct BuildPipeline options."),
            ],
            relatedCodes: [BuildErrorCodes.BuildProfileInvalid, BuildErrorCodes.BuildTargetUnsupported]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildTargetModuleMissing,
            category: "build",
            summary: "The requested build target module is missing.",
            meaning: "Unity recognizes the requested build target, but the current Unity installation does not have the target module required to build it.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["preconditionProbe", "targetModuleCheck"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].message", "payload.build.input.unityBuildTarget", "payload.build.input.unityBuildTargetGroup"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Install the Unity build support module for the requested target, then run the build again."),
            ],
            relatedCodes: [BuildErrorCodes.BuildInputsInvalid]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildRuntimePolicyViolation,
            category: "build",
            summary: "The build runtime policy rejected the selected runtime.",
            meaning: "The resolved execution mode or Unity editor mode is not allowed by the build profile runtime policy.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["runtimePolicy", "preconditionProbe"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].message", "payload.project"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Run the build with an allowed execution/editor mode or update the build profile runtime policy."),
            ],
            relatedCodes: [BuildErrorCodes.BuildProfileInvalid]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildDirtyStatePresent,
            category: "build",
            summary: "Audited project items have unsaved changes.",
            meaning: "One or more loaded scenes or persistent project assets are dirty, so the BuildPipeline precondition probe stopped before starting the build.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["preconditionProbe", "dirtyStateCheck"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].message", "payload.dirtyState.checked", "payload.dirtyState.dirty", "payload.dirtyState.coverage", "payload.dirtyState.items[].path"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Save or revert the dirty project items listed in dirtyState.items before running the build again."),
            ],
            relatedCodes: [BuildErrorCodes.BuildInputsInvalid]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildDirtyStateIndeterminate,
            category: "build",
            summary: "Build dirty state could not be fully checked.",
            meaning: "The build precondition probe did not find dirty items, but dirty-state coverage was partial, so runner invocation was blocked.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["preconditionProbe", "dirtyStateCheck"],
            impliesNotApplied: true,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.Unknown,
            inspect: ["errors[].code", "errors[].message", "payload.dirtyState.checked", "payload.dirtyState.coverage"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect dirtyState.coverage and rerun after Unity can provide complete dirty-state evidence."),
            ],
            relatedCodes: [BuildErrorCodes.BuildDirtyStatePresent]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildReportMissing,
            category: "build",
            summary: "Unity BuildReport is missing.",
            meaning: "Unity BuildPipeline returned without a BuildReport that can be normalized and saved.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["buildPipeline", "buildReportRead"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.Unknown,
            inspect: ["errors[].code", "errors[].message", "payload.build.summary", "payload.reports.buildReport"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect the Unity log and rerun the build after resolving BuildPipeline failures."),
            ],
            relatedCodes: [BuildErrorCodes.BuildInputsInvalid]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildArtifactWriteFailed,
            category: "build",
            summary: "Build artifacts could not be written.",
            meaning: "uCLI could not create or persist one of the build run artifacts under local storage.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["artifactPreparation", "artifactWrite"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.Unknown,
            inspect: ["errors[].code", "errors[].message", "payload.reports"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Check write permissions and remove any conflicting build artifact directory before retrying."),
            ],
            relatedCodes: [UcliCoreErrorCodes.InternalError]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildOutputManifestFailed,
            category: "build",
            summary: "Build output manifest could not be generated.",
            meaning: "uCLI could not enumerate build output files or persist the output manifest artifact.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["outputManifest"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.Unknown,
            inspect: ["errors[].code", "errors[].message", "payload.build.output"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect generated output entries and remove unsupported files such as symlinks before retrying."),
            ],
            relatedCodes: [BuildErrorCodes.BuildArtifactWriteFailed]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildOutputDigestMismatch,
            category: "build",
            summary: "Build output manifest digest mismatch.",
            meaning: "The persisted output manifest digest did not match the digest recalculated from manifest content.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["outputManifest", "artifactAccounting"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.Yes,
            inspect: ["errors[].code", "errors[].message", "payload.build.output.manifestDigest"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Rerun the build and inspect concurrent writes to the build artifact directory if the mismatch repeats."),
            ],
            relatedCodes: [BuildErrorCodes.BuildOutputManifestFailed]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildProjectMutationForbidden,
            category: "build",
            summary: "Project mutation was detected while mutation is forbidden.",
            meaning: "The build profile projectMutationMode is forbid and the project mutation audit detected file changes across runner invocation.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["projectMutationAudit"],
            impliesNotApplied: false,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].message", "payload.project"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Review build.json projectMutation evidence and either remove the mutation or use an audit policy."),
            ],
            relatedCodes: [BuildErrorCodes.BuildRuntimePolicyViolation]),
    ];
}
