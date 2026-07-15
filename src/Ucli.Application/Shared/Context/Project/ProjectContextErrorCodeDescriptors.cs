namespace MackySoft.Ucli.Application.Shared.Context.Project;

internal static class ProjectContextErrorCodeDescriptors
{
    private static readonly UcliCommand[] ProjectResolutionCommands =
    [
        UcliCommandIds.Status,
        UcliCommandIds.Ready,
        UcliCommandIds.Compile,
        UcliCommandIds.BuildRun,
        UcliCommandIds.Verify,
        UcliCommandIds.DaemonStart,
        UcliCommandIds.DaemonStop,
        UcliCommandIds.DaemonCleanup,
        UcliCommandIds.DaemonStatus,
        UcliCommandIds.DaemonList,
        UcliCommandIds.LogsDaemonRead,
        UcliCommandIds.LogsUnityRead,
        UcliCommandIds.LogsUnityClear,
        UcliCommandIds.Screenshot,
        UcliCommandIds.Play,
        UcliCommandIds.Validate,
        UcliCommandIds.Plan,
        UcliCommandIds.Call,
        UcliCommandIds.Eval,
        UcliCommandIds.Resolve,
        UcliCommandIds.Query,
        UcliCommandIds.Refresh,
        UcliCommandIds.Ops,
        UcliCommandIds.TestRun,
    ];

    private static readonly UcliCommand[] ProjectStorageCommands =
    [
        UcliCommandIds.Init,
        .. ProjectResolutionCommands,
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        CreateProjectContextDescriptor(
            ProjectContextErrorCodes.ProjectPathInvalidFormat,
            "The project path cannot be normalized.",
            "The project path argument is not a valid filesystem path for this host.",
            "projectPathParsing",
            "Pass a valid Unity project directory and rerun the command.",
            ProjectResolutionCommands),

        CreateProjectContextDescriptor(
            ProjectContextErrorCodes.ProjectPathNotFound,
            "The project path does not exist.",
            "The resolved project directory is missing, so uCLI cannot establish project context.",
            "projectPathResolution",
            "Pass a valid Unity project directory and rerun the command.",
            ProjectResolutionCommands),

        CreateProjectContextDescriptor(
            ProjectContextErrorCodes.ProjectStorageRootTooLong,
            "The repository storage root is too long.",
            "The normalized repository root exceeds the Windows path budget supported by uCLI local storage.",
            "projectStorageResolution",
            "Move the repository to a shorter path and rerun the command.",
            ProjectStorageCommands),

        CreateProjectContextDescriptor(
            ProjectContextErrorCodes.UnityProjectMarkerMissing,
            "The directory is not recognized as a Unity project.",
            "The resolved directory is missing Unity project markers required by uCLI.",
            "projectValidation",
            "Pass a valid Unity project directory and rerun the command.",
            ProjectResolutionCommands),
    ];

    private static UcliErrorDescriptor CreateProjectContextDescriptor (
        UcliCode code,
        string summary,
        string meaning,
        string phase,
        string nextAction,
        IReadOnlyList<UcliCommand> appliesTo)
    {
        return UcliErrorDescriptorFactory.Create(
            code: code,
            category: "projectContext",
            summary: summary,
            meaning: meaning,
            appliesTo: appliesTo,
            possiblePhases: [phase],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClass.No,
            inspect: ["projectPath", "cwd", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: nextAction),
            ],
            relatedCodes: ProjectContextErrorCodes.All
                .Where(relatedCode => relatedCode != code)
                .ToArray());
    }
}
