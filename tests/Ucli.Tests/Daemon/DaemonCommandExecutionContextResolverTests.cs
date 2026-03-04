using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonCommandExecutionContextResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenDaemonCommandTimeoutOverrideExists_UsesDaemonOverride ()
    {
        var config = UcliConfig.CreateDefault() with
        {
            IpcDefaultTimeoutMilliseconds = 1111,
            IpcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [UcliCommandNames.Daemon] = 2222,
            },
        };
        var initStatusContext = CreateContext(config);
        var initStatusContextResolver = new StubInitStatusContextResolver(
            InitStatusContextResolutionResult.Success(initStatusContext));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        var result = await resolver.Resolve(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

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
                [UcliCommandNames.Daemon] = 2222,
            },
        };
        var initStatusContext = CreateContext(config);
        var initStatusContextResolver = new StubInitStatusContextResolver(
            InitStatusContextResolutionResult.Success(initStatusContext));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        var result = await resolver.Resolve(projectPath: null, timeout: "3333", cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<DaemonCommandExecutionContext>(result.Context);
        Assert.Equal(3333, context.Timeout.TotalMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenTimeoutOptionIsInvalid_ReturnsInvalidArgument ()
    {
        var initStatusContextResolver = new StubInitStatusContextResolver(
            InitStatusContextResolutionResult.Success(CreateContext(UcliConfig.CreateDefault())));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        var result = await resolver.Resolve(projectPath: null, timeout: "abc", cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
    }

    private static InitStatusContext CreateContext (UcliConfig config)
    {
        return new InitStatusContext(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: "/tmp/unity-project",
                RepositoryRoot: "/tmp/repo-root",
                ProjectFingerprint: "fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Config: config,
            ConfigSource: ConfigSource.Default);
    }

    private sealed class StubInitStatusContextResolver : IInitStatusContextResolver
    {
        private readonly InitStatusContextResolutionResult result;

        public StubInitStatusContextResolver (InitStatusContextResolutionResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public ValueTask<InitStatusContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }
}