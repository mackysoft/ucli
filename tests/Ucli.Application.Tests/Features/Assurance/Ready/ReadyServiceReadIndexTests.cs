using MackySoft.Ucli.Application.Features.Assurance;
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
        Assert.Equal(ReadyVerdictValues.Pass, output.Verdict);
        Assert.Equal(AssuranceExecutionModeCodec.NotApplicable, output.ResolvedMode);
        Assert.Equal(AssuranceSessionKindValues.ArtifactOnly, output.SessionKind);
        Assert.Null(output.Lifecycle);
        Assert.NotNull(output.ReadIndex);
        Assert.Equal("allowStale", output.ReadIndex.Mode);
        Assert.Equal(
            ["ops.catalog", "asset-search.lookup", "guid-path.lookup"],
            output.ReadIndex.Artifacts.Select(static artifact => artifact.Name));
        Assert.All(output.ReadIndex.Artifacts, static artifact => Assert.True(artifact.Required));
        var claim = Assert.Single(output.Claims);
        Assert.Equal(ReadyValidityKindValues.ProbeOnly, claim.Validity.Kind);
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
        Assert.Equal(ReadyVerdictValues.Fail, output.Verdict);
        var claim = Assert.Single(output.Claims);
        Assert.Equal(ReadyClaimStatusValues.Failed, claim.Status);

        var artifact = Assert.Single(
            output.ReadIndex!.Artifacts,
            static artifact => string.Equals(artifact.Name, "asset-search.lookup", StringComparison.Ordinal));
        Assert.True(artifact.Required);
        Assert.Equal(ReadyReadIndexArtifactStatusValues.Failed, artifact.Status);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexBootstrapFailed.Value, artifact.Code);
        Assert.Contains("query assets find", artifact.ActionRequired, StringComparison.Ordinal);
    }
}
