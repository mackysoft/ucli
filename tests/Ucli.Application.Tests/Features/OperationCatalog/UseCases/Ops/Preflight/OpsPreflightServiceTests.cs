using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;

namespace MackySoft.Ucli.Application.Tests.Ops.Preflight;

public sealed class OpsPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenInputsAreValid_ReturnsResolvedContext ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject(
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
        var service = new OpsPreflightService(new StaticProjectContextResolver(
            ProjectContextResolutionResult.Success(context)));

        var result = await service.ExecuteAsync(
            new OpsPreflightInput(
                ProjectPath: null,
                Mode: NormalizeMode("daemon"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                ReadIndexMode: NormalizeReadIndexMode("allowStale"),
                FailFast: true));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Context);
        Assert.Same(context, result.Context.Context);
        Assert.Equal(ReadIndexMode.AllowStale, result.Context.ReadIndexMode);
        Assert.Equal(UnityExecutionMode.Daemon, result.Context.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(1200), result.Context.Timeout);
        Assert.True(result.Context.FailFast);
    }

}
