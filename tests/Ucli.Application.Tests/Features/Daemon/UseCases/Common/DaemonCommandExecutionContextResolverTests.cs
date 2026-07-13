using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonCommandExecutionContextResolverTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

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
        var initStatusContext = ProjectContextTestFactory.CreateDaemonLifecycleProject(
            config,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"));
        var initStatusContextResolver = new StaticProjectContextResolver(
            ProjectContextResolutionResult.Success(initStatusContext));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        var result = await resolver.ResolveAsync(
            timeoutCommand: UcliCommandIds.DaemonStart,
            projectPath: null,
            timeoutMilliseconds: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<DaemonCommandExecutionContext>(result.Context);
        Assert.Equal(2222, context.Timeout.TotalMilliseconds);
        ProjectContextResolverAssert.ProjectContextResolvedOnce(
            initStatusContextResolver,
            expectedProjectPath: null,
            CancellationToken.None);
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
        var initStatusContext = ProjectContextTestFactory.CreateDaemonLifecycleProject(
            config,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"));
        var initStatusContextResolver = new StaticProjectContextResolver(
            ProjectContextResolutionResult.Success(initStatusContext));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        var result = await resolver.ResolveAsync(
            timeoutCommand: UcliCommandIds.DaemonStop,
            projectPath: null,
            timeoutMilliseconds: 3333,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<DaemonCommandExecutionContext>(result.Context);
        Assert.Equal(3333, context.Timeout.TotalMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenTimeoutValueIsInvalid_ReturnsInvalidArgument ()
    {
        var initStatusContextResolver = new StaticProjectContextResolver(
            ProjectContextResolutionResult.Success(ProjectContextTestFactory.CreateDaemonLifecycleProject(
                projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"))));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        var result = await resolver.ResolveAsync(
            timeoutCommand: UcliCommandIds.DaemonStatus,
            projectPath: null,
            timeoutMilliseconds: 0,
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
        var initStatusContextResolver = new StaticProjectContextResolver(
            ProjectContextResolutionResult.Success(ProjectContextTestFactory.CreateDaemonLifecycleProject(
                projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"))));
        var resolver = new DaemonCommandExecutionContextResolver(initStatusContextResolver);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                resolver.ResolveAsync(
                    timeoutCommand: default,
                    projectPath: null,
                    timeoutMilliseconds: null,
                    cancellationToken: CancellationToken.None).AsTask(),
                "Invalid daemon timeout command resolution",
                AsyncWaitTimeout);
        });
    }
}
