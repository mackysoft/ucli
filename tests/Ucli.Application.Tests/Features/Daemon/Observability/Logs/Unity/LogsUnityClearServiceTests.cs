using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Tests.Daemon;

namespace MackySoft.Ucli.Application.Tests.Logs;

public sealed class LogsUnityClearServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenClientSucceeds_ReturnsClearedOutputAndUsesClearTimeoutCommand ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 4500);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var client = new StubUnityConsoleClearClient(UnityConsoleClearClientResult.Success());
        var service = new LogsUnityClearService(resolver, client);

        var result = await service.ExecuteAsync(
            new LogsUnityClearServiceRequest(
                ProjectPath: "/tmp/unity-project",
                TimeoutMilliseconds: 4500),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("cleared", result.Output!.ClearStatus);
        Assert.Equal(4500, result.Output.TimeoutMilliseconds);
        Assert.Equal(UcliCommandIds.LogsUnityClear, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/unity-project", resolver.LastProjectPath);
        Assert.Equal(4500, resolver.LastTimeoutMilliseconds);
        Assert.Equal(context.Context.UnityProject, client.LastUnityProject);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), client.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenClientFails_ReturnsClientError ()
    {
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(
                DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000)));
        var client = new StubUnityConsoleClearClient(UnityConsoleClearClientResult.Failure(ExecutionError.InvalidArgument("GUI Editor daemon is required.")));
        var service = new LogsUnityClearService(resolver, client);

        var result = await service.ExecuteAsync(
            new LogsUnityClearServiceRequest(
                ProjectPath: null,
                TimeoutMilliseconds: null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("GUI Editor daemon", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenContextResolutionFails_DoesNotCallClient ()
    {
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(ExecutionError.InvalidArgument("projectPath is invalid.")));
        var client = new StubUnityConsoleClearClient(UnityConsoleClearClientResult.Success());
        var service = new LogsUnityClearService(resolver, client);

        var result = await service.ExecuteAsync(
            new LogsUnityClearServiceRequest(
                ProjectPath: "missing",
                TimeoutMilliseconds: null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, client.CallCount);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    private sealed class StubUnityConsoleClearClient : IUnityConsoleClearClient
    {
        private readonly UnityConsoleClearClientResult result;

        public StubUnityConsoleClearClient (UnityConsoleClearClientResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public ValueTask<UnityConsoleClearClientResult> ClearAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            return ValueTask.FromResult(result);
        }
    }
}
