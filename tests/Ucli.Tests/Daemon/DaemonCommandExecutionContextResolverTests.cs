using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonCommandExecutionContextResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenDaemonSubcommandTimeoutOverrideExists_UsesOverride ()
    {
        var config = UcliConfig.CreateDefault() with
        {
            IpcDefaultTimeoutMilliseconds = 1111,
            IpcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [UcliCommandIds.DaemonStart.Name] = 2222,
            },
        };
        var initStatusContext = CreateContext(config);
        var initStatusContextResolver = new StubProjectContextResolver(
            ProjectContextResolutionResult.Success(initStatusContext));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        var result = await resolver.Resolve(
            timeoutCommand: UcliCommandIds.DaemonStart,
            projectPath: null,
            timeout: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<DaemonCommandExecutionContext>(result.Context);
        Assert.Equal(2222, context.Timeout.TotalMilliseconds);
        Assert.Equal(1, initStatusContextResolver.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenTimeoutOptionIsSpecified_UsesOptionValue ()
    {
        var config = UcliConfig.CreateDefault() with
        {
            IpcDefaultTimeoutMilliseconds = 1111,
            IpcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [UcliCommandIds.DaemonStop.Name] = 2222,
            },
        };
        var initStatusContext = CreateContext(config);
        var initStatusContextResolver = new StubProjectContextResolver(
            ProjectContextResolutionResult.Success(initStatusContext));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        var result = await resolver.Resolve(
            timeoutCommand: UcliCommandIds.DaemonStop,
            projectPath: null,
            timeout: "3333",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<DaemonCommandExecutionContext>(result.Context);
        Assert.Equal(3333, context.Timeout.TotalMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenTimeoutOptionIsInvalid_ReturnsInvalidArgument ()
    {
        var initStatusContextResolver = new StubProjectContextResolver(
            ProjectContextResolutionResult.Success(CreateContext(UcliConfig.CreateDefault())));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        var result = await resolver.Resolve(
            timeoutCommand: UcliCommandIds.DaemonStatus,
            projectPath: null,
            timeout: "abc",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenTimeoutCommandNameIsInvalid_ThrowsArgumentException ()
    {
        var initStatusContextResolver = new StubProjectContextResolver(
            ProjectContextResolutionResult.Success(CreateContext(UcliConfig.CreateDefault())));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await resolver.Resolve(
                timeoutCommand: default,
                projectPath: null,
                timeout: null,
                cancellationToken: CancellationToken.None);
        });
    }

    private static ProjectContext CreateContext (UcliConfig config)
    {
        return new ProjectContext(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: "/tmp/unity-project",
                RepositoryRoot: "/tmp/repo-root",
                ProjectFingerprint: "fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Config: config,
            ConfigSource: ConfigSource.Default);
    }

    private sealed class StubProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult result;

        public StubProjectContextResolver (ProjectContextResolutionResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public ValueTask<ProjectContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }
}