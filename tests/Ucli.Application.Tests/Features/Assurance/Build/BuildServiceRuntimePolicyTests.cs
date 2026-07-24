using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceRuntimePolicyTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithAutoResolvedDaemonRejectedByRuntimePolicy_DoesNotBypassDaemon ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(
                CreateProfileJson(["oneshot"], ["batchmode", "gui"], "forbid"),
                DefaultBuildProfilePath)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: true,
                UnityExecutionTarget.Daemon,
                TimeSpan.FromSeconds(10)))),
            requestExecutor: new UnexpectedUnityRequestExecutor(),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRuntimePolicyViolation, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithOneshotBatchmodeRejectedByRuntimePolicy_ReturnsRuntimePolicyViolation ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(
                CreateProfileJson(["oneshot"], ["gui"], "forbid"),
                DefaultBuildProfilePath)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Oneshot,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            requestExecutor: new UnexpectedUnityRequestExecutor(),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRuntimePolicyViolation, error.Code);
    }
}
