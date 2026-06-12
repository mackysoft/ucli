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
            code: BuildErrorCodes.BuildDirtyStatePresent,
            category: "build",
            summary: "Build input scenes have unsaved changes.",
            meaning: "One or more loaded scenes included in the build input are dirty, so the BuildPipeline precondition probe stopped before starting the build.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["preconditionProbe", "dirtyStateCheck"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].message", "payload.dirtyState.checked", "payload.dirtyState.dirty", "payload.dirtyState.items[].path"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Save or revert the dirty scenes listed in dirtyState.items before running the build again."),
            ],
            relatedCodes: [BuildErrorCodes.BuildInputsInvalid]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildArtifactWriteFailed,
            category: "build",
            summary: "The build artifacts could not be written.",
            meaning: "uCLI could not create or update the build-run artifact directory or one of its required metadata artifacts.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["artifactPreparation", "artifactWrite"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.Yes,
            inspect: ["errors[].code", "errors[].message", "payload.build.artifacts"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Check filesystem permissions and remove any conflicting file or symbolic link under the build artifact directory."),
            ],
            relatedCodes: [UcliCoreErrorCodes.InternalError]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildOutputManifestFailed,
            category: "build",
            summary: "The build output manifest could not be produced.",
            meaning: "uCLI could not enumerate the build output directory or persist output-manifest.json with trustworthy file accounting.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["outputManifestWrite", "artifactAccounting"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.Yes,
            inspect: ["errors[].code", "errors[].message", "payload.build.artifacts.buildOutputManifest"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Check the generated build output directory for unsupported links, permissions, or files that are still being modified."),
            ],
            relatedCodes: [BuildErrorCodes.BuildArtifactWriteFailed]),

        UcliErrorDescriptorFactory.Create(
            code: BuildErrorCodes.BuildOutputDigestMismatch,
            category: "build",
            summary: "The build output digest accounting changed while being recorded.",
            meaning: "A build output file changed while uCLI was calculating output-manifest.json, so the manifest could not be trusted.",
            appliesTo: AppliesToBuildRun,
            possiblePhases: ["artifactAccounting"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.Yes,
            inspect: ["errors[].code", "errors[].message", "payload.build.artifacts.buildOutputManifest"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Retry the build after ensuring no external process mutates the build output directory."),
            ],
            relatedCodes: [BuildErrorCodes.BuildOutputManifestFailed]),
    ];
}
