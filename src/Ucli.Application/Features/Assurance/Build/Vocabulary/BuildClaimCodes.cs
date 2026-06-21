namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines assurance claim codes emitted by the <c>build.run</c> command. </summary>
internal static class BuildClaimCodes
{
    public static readonly UcliCode UnityBuildProfileResolved = new("UNITY_BUILD_PROFILE_RESOLVED");

    public static readonly UcliCode UnityReadyForBuild = new("UNITY_READY_FOR_BUILD");

    public static readonly UcliCode UnityBuildInputsResolved = new("UNITY_BUILD_INPUTS_RESOLVED");

    public static readonly UcliCode UnityBuildRunnerResolved = new("UNITY_BUILD_RUNNER_RESOLVED");

    public static readonly UcliCode UnityBuildExecuteMethodResolved = new("UNITY_BUILD_EXECUTE_METHOD_RESOLVED");

    public static readonly UcliCode UnityBuildExecuteMethodInvoked = new("UNITY_BUILD_EXECUTE_METHOD_INVOKED");

    public static readonly UcliCode UnityBuildExecuteMethodCompleted = new("UNITY_BUILD_EXECUTE_METHOD_COMPLETED");

    public static readonly UcliCode UnityBuildCompleted = new("UNITY_BUILD_COMPLETED");

    public static readonly UcliCode UnityBuildSucceeded = new("UNITY_BUILD_SUCCEEDED");

    public static readonly UcliCode UnityBuildResultAccounted = new("UNITY_BUILD_RESULT_ACCOUNTED");

    public static readonly UcliCode UnityBuildReportAccounted = new("UNITY_BUILD_REPORT_ACCOUNTED");

    public static readonly UcliCode UnityBuildArtifactsAccounted = new("UNITY_BUILD_ARTIFACTS_ACCOUNTED");

    public static readonly UcliCode UnityBuildOutputDigested = new("UNITY_BUILD_OUTPUT_DIGESTED");

    public static readonly UcliCode UnityBuildLogsAccounted = new("UNITY_BUILD_LOGS_ACCOUNTED");

    public static readonly UcliCode UnityBuildProjectMutationAccounted = new("UNITY_BUILD_PROJECT_MUTATION_ACCOUNTED");

    public static readonly UcliCode UnityBuildValidForGeneration = new("UNITY_BUILD_VALID_FOR_GENERATION");

    public static IReadOnlyList<UcliCode> All { get; } =
    [
        UnityBuildProfileResolved,
        UnityReadyForBuild,
        UnityBuildInputsResolved,
        UnityBuildRunnerResolved,
        UnityBuildCompleted,
        UnityBuildSucceeded,
        UnityBuildResultAccounted,
        UnityBuildReportAccounted,
        UnityBuildArtifactsAccounted,
        UnityBuildOutputDigested,
        UnityBuildLogsAccounted,
        UnityBuildProjectMutationAccounted,
        UnityBuildValidForGeneration,
    ];
}
