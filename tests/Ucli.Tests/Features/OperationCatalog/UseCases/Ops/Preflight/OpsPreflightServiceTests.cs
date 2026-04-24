using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Features.OperationCatalog.UseCases.Ops;
using MackySoft.Ucli.Features.OperationCatalog.UseCases.Ops.Preflight;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using static MackySoft.Ucli.Tests.Helpers.Cli.CommandOptionNormalizationTestHelper;

namespace MackySoft.Ucli.Tests.Ops.Preflight;

public sealed class OpsPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenInputsAreValid_ReturnsResolvedContext ()
    {
        var context = CreateContext();
        var service = new OpsPreflightService(new StubProjectContextResolver(
            ProjectContextResolutionResult.Success(context)));

        var result = await service.Execute(
            new OpsCommandInput(
                ProjectPath: null,
                Mode: NormalizeMode("daemon"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                ReadIndexMode: NormalizeReadIndexMode(ReadIndexModeValues.AllowStale),
                FailFast: true));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Context);
        Assert.Same(context, result.Context.Context);
        Assert.Equal(ReadIndexMode.AllowStale, result.Context.ReadIndexMode);
        Assert.Equal(UnityExecutionMode.Daemon, result.Context.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(1200), result.Context.Timeout);
        Assert.True(result.Context.FailFast);
    }

    private static ProjectContext CreateContext ()
    {
        return new ProjectContext(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/repo/UnityProject",
                RepositoryRoot: "/repo",
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
    }

    private sealed class StubProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult result;

        public StubProjectContextResolver (ProjectContextResolutionResult result)
        {
            this.result = result;
        }

        public ValueTask<ProjectContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}