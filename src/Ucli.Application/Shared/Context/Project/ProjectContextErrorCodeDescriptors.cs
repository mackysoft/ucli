namespace MackySoft.Ucli.Application.Shared.Context.Project;

internal static class ProjectContextErrorCodeDescriptors
{
    private static readonly UcliCommand[] ProjectCommands =
    [
        UcliCommandIds.Init,
        UcliCommandIds.Status,
        UcliCommandIds.DaemonStart,
        UcliCommandIds.DaemonStop,
        UcliCommandIds.DaemonCleanup,
        UcliCommandIds.DaemonStatus,
        UcliCommandIds.DaemonList,
        UcliCommandIds.LogsDaemonRead,
        UcliCommandIds.LogsUnityRead,
        UcliCommandIds.LogsUnityClear,
        UcliCommandIds.Validate,
        UcliCommandIds.Plan,
        UcliCommandIds.Call,
        UcliCommandIds.Resolve,
        UcliCommandIds.Query,
        UcliCommandIds.Refresh,
        UcliCommandIds.Ops,
        UcliCommandIds.TestRun,
        UcliCommandIds.TestProfileInit,
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        CreateProjectContextDescriptor(
            ProjectContextErrorCodes.ProjectPathInvalidFormat,
            "The project path cannot be normalized.",
            "The project path argument is not a valid filesystem path for this host.",
            "projectPathParsing"),

        CreateProjectContextDescriptor(
            ProjectContextErrorCodes.ProjectPathNotFound,
            "The project path does not exist.",
            "The resolved project directory is missing, so uCLI cannot establish project context.",
            "projectPathResolution"),

        CreateProjectContextDescriptor(
            ProjectContextErrorCodes.UnityProjectMarkerMissing,
            "The directory is not recognized as a Unity project.",
            "The resolved directory is missing Unity project markers required by uCLI.",
            "projectValidation"),
    ];

    private static UcliErrorDescriptor CreateProjectContextDescriptor (
        UcliCode code,
        string summary,
        string meaning,
        string phase)
    {
        return UcliErrorDescriptorFactory.Create(
            code: code,
            category: "projectContext",
            summary: summary,
            meaning: meaning,
            appliesTo: ProjectCommands,
            possiblePhases: [phase],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["projectPath", "cwd", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Pass a valid Unity project directory and rerun the command."),
            ],
            relatedCodes: new[]
            {
                ProjectContextErrorCodes.ProjectPathInvalidFormat,
                ProjectContextErrorCodes.ProjectPathNotFound,
                ProjectContextErrorCodes.UnityProjectMarkerMissing,
            }.Where(relatedCode => relatedCode != code).ToArray());
    }
}
