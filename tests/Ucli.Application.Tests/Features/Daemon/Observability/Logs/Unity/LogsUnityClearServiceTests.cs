using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Logs;

public sealed class LogsUnityClearServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenClientSucceeds_ReturnsClearedOutputAndUsesClearTimeoutCommand ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 4500);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var client = new RecordingUnityConsoleClearClient(UnityConsoleClearClientResult.Success());
        var service = new LogsUnityClearService(resolver, client);

        var result = await service.ExecuteAsync(
            new LogsUnityClearServiceRequest(
                ProjectPath: "/tmp/unity-project",
                TimeoutMilliseconds: 4500),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("cleared", result.Output!.ClearStatus);
        Assert.Equal(4500, result.Output.TimeoutMilliseconds);
        DaemonCommandExecutionContextResolverAssert.ResolvedFor(
            resolver,
            UcliCommandIds.LogsUnityClear,
            expectedProjectPath: "/tmp/unity-project",
            expectedTimeoutMilliseconds: 4500,
            expectedCancellationToken: CancellationToken.None);
        UnityConsoleClearClientAssert.ClearRequestedOnce(
            client,
            context,
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenClientFails_ReturnsClientError ()
    {
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(
                DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 3000)));
        var client = new RecordingUnityConsoleClearClient(UnityConsoleClearClientResult.Failure(ExecutionError.InvalidArgument("GUI Editor daemon is required.")));
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
    public async Task Execute_WhenContextResolutionFails_ReturnsFailureBeforeClearRequest ()
    {
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(ExecutionError.InvalidArgument("projectPath is invalid.")));
        var client = new UnexpectedUnityConsoleClearClient("Unity console clear should not be requested when context resolution fails.");
        var service = new LogsUnityClearService(resolver, client);

        var result = await service.ExecuteAsync(
            new LogsUnityClearServiceRequest(
                ProjectPath: "missing",
                TimeoutMilliseconds: null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }
}
