using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

public sealed class AssuranceEffectSetsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateBuild_UsesRunnerKindAndBuildReportPresence ()
    {
        var buildPipelineEffects = AssuranceEffectSets.CreateBuild(
            BuildRunnerKind.BuildPipeline,
            hasBuildReport: true);
        var executeMethodEffects = AssuranceEffectSets.CreateBuild(
            BuildRunnerKind.ExecuteMethod,
            hasBuildReport: false);

        Assert.Equal(
            [
                AssuranceEffect.UnityLifecycleRead,
                AssuranceEffect.UnityBuildPipeline,
                AssuranceEffect.UnityBuildReportRead,
                AssuranceEffect.UnityLogWindowRead,
                AssuranceEffect.UcliArtifactWrite,
                AssuranceEffect.OutputManifestWrite,
                AssuranceEffect.GenerationSnapshot,
                AssuranceEffect.ProjectMutationAudit,
            ],
            buildPipelineEffects);
        Assert.Equal(
            [
                AssuranceEffect.UnityLifecycleRead,
                AssuranceEffect.UnityExecuteMethod,
                AssuranceEffect.UnityLogWindowRead,
                AssuranceEffect.UcliArtifactWrite,
                AssuranceEffect.OutputManifestWrite,
                AssuranceEffect.GenerationSnapshot,
                AssuranceEffect.ProjectMutationAudit,
            ],
            executeMethodEffects);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateBuild_ReturnsCachedImmutableEffectSet ()
    {
        var first = AssuranceEffectSets.CreateBuild(
            BuildRunnerKind.BuildPipeline,
            hasBuildReport: true);
        var second = AssuranceEffectSets.CreateBuild(
            BuildRunnerKind.BuildPipeline,
            hasBuildReport: true);
        var list = Assert.IsAssignableFrom<IList<AssuranceEffect>>(first);

        Assert.Same(first, second);
        Assert.Throws<NotSupportedException>(() => list[0] = AssuranceEffect.ProjectMutationAudit);
        Assert.Equal(AssuranceEffect.UnityLifecycleRead, first[0]);
    }
}
