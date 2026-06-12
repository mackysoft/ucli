using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Diagnostics;

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
