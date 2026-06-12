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
    ];
}
