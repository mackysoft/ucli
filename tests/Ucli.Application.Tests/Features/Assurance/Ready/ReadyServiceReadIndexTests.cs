using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Ready;

using static MackySoft.Ucli.Application.Tests.Features.Assurance.Ready.ReadyServiceTestSupport;

public sealed class ReadyServiceReadIndexTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithReadIndexTarget_ReadsArtifactsWithoutUnityLifecycleProbe ()
    {
        var service = CreateService(
            modeDecisionService: new UnexpectedModeDecisionService(),
            daemonPingInfoClient: new UnexpectedDaemonPingInfoClient("Read-index readiness must not probe daemon lifecycle."),
            unityRequestExecutor: new UnexpectedUnityRequestExecutor(),
            readIndexArtifactReader: ReadyReadIndexArtifactReaderFactory.CreateReadyArtifacts());

        var result = await service.ExecuteAsync(CreateReadIndexInput(readIndexMode: ReadIndexMode.AllowStale));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<ReadyExecutionOutput>(result.Output);
        Assert.Equal(AssuranceVerdict.Pass, output.Verdict);
        Assert.Equal(AssuranceResolvedExecutionMode.NotApplicable, output.ResolvedMode);
        Assert.Equal(AssuranceSessionKind.ArtifactOnly, output.SessionKind);
        Assert.Null(output.Lifecycle);
        Assert.NotNull(output.ReadIndex);
        Assert.Equal(ReadyReadIndexMode.AllowStale, output.ReadIndex.Mode);
        Assert.Equal(
            [ReadyReadIndexArtifactName.OpsCatalog, ReadyReadIndexArtifactName.AssetSearchLookup, ReadyReadIndexArtifactName.GuidPathLookup],
            output.ReadIndex.Artifacts.Select(static artifact => artifact.Name));
        Assert.All(output.ReadIndex.Artifacts, static artifact => Assert.True(artifact.Required));
        var claim = Assert.Single(output.Claims);
        Assert.Equal(ReadyValidityKind.ProbeOnly, claim.Validity.Kind);
        Assert.False(claim.Validity.GuaranteesReusableSession);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithReadIndexTarget_WhenRequiredArtifactIsMissing_ReturnsFailedClaimWithActionRequired ()
    {
        var service = CreateService(
            readIndexArtifactReader: ReadyReadIndexArtifactReaderFactory.CreateReadyArtifacts("asset-search.lookup"));

        var result = await service.ExecuteAsync(CreateReadIndexInput(readIndexMode: ReadIndexMode.RequireFresh));

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<ReadyExecutionOutput>(result.Output);
        Assert.Equal(AssuranceVerdict.Fail, output.Verdict);
        var claim = Assert.Single(output.Claims);
        Assert.Equal(AssuranceClaimStatus.Failed, claim.Status);

        var artifact = Assert.Single(
            output.ReadIndex!.Artifacts,
            static artifact => artifact.Name == ReadyReadIndexArtifactName.AssetSearchLookup);
        Assert.True(artifact.Required);
        Assert.Equal(ReadyReadIndexArtifactStatus.Failed, artifact.Status);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexBootstrapFailed, artifact.Code);
        Assert.Contains("query assets find", artifact.ActionRequired, StringComparison.Ordinal);
    }
}
