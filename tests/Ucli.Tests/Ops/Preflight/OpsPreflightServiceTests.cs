using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Ops;
using MackySoft.Ucli.Ops.Preflight;
using MackySoft.Ucli.ReadIndex;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Ops.Preflight;

public sealed class OpsPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenModeIsInvalid_ReturnsInvalidArgument ()
    {
        var service = new OpsPreflightService(new StubProjectContextResolver(
            ProjectContextResolutionResult.Success(CreateContext())));

        var result = await service.Execute(
            new OpsCommandInput(
                ProjectPath: null,
                Mode: "unsupported",
                Timeout: null,
                ReadIndexMode: null));

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_ARGUMENT", result.ErrorCode);
        Assert.Contains("Mode must be auto, daemon, or oneshot.", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTimeoutIsInvalid_ReturnsInvalidArgument ()
    {
        var service = new OpsPreflightService(new StubProjectContextResolver(
            ProjectContextResolutionResult.Success(CreateContext())));

        var result = await service.Execute(
            new OpsCommandInput(
                ProjectPath: null,
                Mode: null,
                Timeout: "abc",
                ReadIndexMode: null));

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_ARGUMENT", result.ErrorCode);
        Assert.Contains("timeout", result.Message, StringComparison.Ordinal);
    }

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
                Mode: "daemon",
                Timeout: "1200",
                ReadIndexMode: ReadIndexModeValues.AllowStale));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Context);
        Assert.Same(context, result.Context.Context);
        Assert.Equal(ReadIndexMode.AllowStale, result.Context.ReadIndexMode);
        Assert.Equal(UnityExecutionMode.Daemon, result.Context.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(1200), result.Context.Timeout);
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