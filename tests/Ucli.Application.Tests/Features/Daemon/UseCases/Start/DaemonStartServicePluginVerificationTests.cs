using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartServicePluginVerificationTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenUnityPluginMarkerIsMissing_ReturnsInvalidArgumentBeforeSupervisorBootstrap ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 1200);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var pluginVerifier = new RecordingUnityPluginVerifier
        {
            Result = UnityPluginVerificationResult.Failure(ExecutionError.InvalidArgument(
                "Unity project does not contain the uCLI Unity plugin.")),
        };
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway();
        var progressSink = new CollectingCommandProgressSink();
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway, pluginVerifier);

        var result = await service.StartAsync(
            projectPath: null,
            timeoutMilliseconds: null,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink: progressSink,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        DaemonStartServiceAssert.PluginVerificationFailureStoppedBeforeSupervisorBootstrap(
            pluginVerifier,
            supervisorProjectGateway,
            context.Context.UnityProject.UnityProjectRoot);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed));
        DaemonStartProgressAssert.PluginVerificationFailurePayloads(
            progressSink,
            expectedErrorCode: "INVALID_ARGUMENT");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenUnityPluginVerificationExceedsTimeout_ReturnsTimeoutBeforeSupervisorBootstrap ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 120);
        var timeProvider = new ManualTimeProvider();
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway();
        var pluginVerifier = new RecordingUnityPluginVerifier
        {
            Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            Handler = async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return UnityPluginVerificationResult.Success();
            },
        };
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway, pluginVerifier, timeProvider: timeProvider);

        var resultTask = service.StartAsync(projectPath: null, timeoutMilliseconds: null, editorMode: null, onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto, cancellationToken: CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(pluginVerifier.Started!.Task, "Unity plugin verification start", SignalWaitTimeout);
        timeProvider.Advance(context.Timeout);

        var result = await TestAwaiter.WaitAsync(resultTask, "Unity plugin verification timeout result", SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        DaemonStartServiceAssert.PluginVerificationTimeoutStoppedBeforeSupervisorBootstrap(
            pluginVerifier,
            supervisorProjectGateway);
    }
}
