using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Defines the effect sets computed by the built-in assurance verifiers. </summary>
internal static class AssuranceEffectSets
{
    private static readonly IReadOnlyList<AssuranceEffect> BuildPipelineWithReport = CreateBuildSet(
        AssuranceEffect.UnityBuildPipeline,
        hasBuildReport: true);

    private static readonly IReadOnlyList<AssuranceEffect> BuildPipelineWithoutReport = CreateBuildSet(
        AssuranceEffect.UnityBuildPipeline,
        hasBuildReport: false);

    private static readonly IReadOnlyList<AssuranceEffect> ExecuteMethodWithReport = CreateBuildSet(
        AssuranceEffect.UnityExecuteMethod,
        hasBuildReport: true);

    private static readonly IReadOnlyList<AssuranceEffect> ExecuteMethodWithoutReport = CreateBuildSet(
        AssuranceEffect.UnityExecuteMethod,
        hasBuildReport: false);

    public static IReadOnlyList<AssuranceEffect> CreateBuild (
        BuildRunnerKind runnerKind,
        bool hasBuildReport)
    {
        return (runnerKind, hasBuildReport) switch
        {
            (BuildRunnerKind.BuildPipeline, true) => BuildPipelineWithReport,
            (BuildRunnerKind.BuildPipeline, false) => BuildPipelineWithoutReport,
            (BuildRunnerKind.ExecuteMethod, true) => ExecuteMethodWithReport,
            (BuildRunnerKind.ExecuteMethod, false) => ExecuteMethodWithoutReport,
            _ => throw new ArgumentOutOfRangeException(nameof(runnerKind), runnerKind, "Build runner kind must be defined by the assurance contract."),
        };
    }

    public static IReadOnlyList<AssuranceEffect> Compile { get; } = Array.AsReadOnly(
        new[]
        {
            AssuranceEffect.AssetDatabaseRefresh,
            AssuranceEffect.ScriptCompilation,
            AssuranceEffect.DomainReload,
        });

    public static IReadOnlyList<AssuranceEffect> Test { get; } = Array.AsReadOnly(
        new[]
        {
            AssuranceEffect.UnityTestRunner,
        });

    public static IReadOnlyList<AssuranceEffect> None { get; } = Array.Empty<AssuranceEffect>();

    private static IReadOnlyList<AssuranceEffect> CreateBuildSet (
        AssuranceEffect runnerEffect,
        bool hasBuildReport)
    {
        var effects = hasBuildReport
            ? new[]
            {
                AssuranceEffect.UnityLifecycleRead,
                runnerEffect,
                AssuranceEffect.UnityBuildReportRead,
                AssuranceEffect.UnityLogWindowRead,
                AssuranceEffect.UcliArtifactWrite,
                AssuranceEffect.OutputManifestWrite,
                AssuranceEffect.GenerationSnapshot,
                AssuranceEffect.ProjectMutationAudit,
            }
            :
            [
                AssuranceEffect.UnityLifecycleRead,
                runnerEffect,
                AssuranceEffect.UnityLogWindowRead,
                AssuranceEffect.UcliArtifactWrite,
                AssuranceEffect.OutputManifestWrite,
                AssuranceEffect.GenerationSnapshot,
                AssuranceEffect.ProjectMutationAudit,
            ];

        return Array.AsReadOnly(effects);
    }
}
