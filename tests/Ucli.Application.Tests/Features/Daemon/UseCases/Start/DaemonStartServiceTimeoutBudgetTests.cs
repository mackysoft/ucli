using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartServiceTimeoutBudgetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenPluginVerificationConsumesBudget_PropagatesRemainingTimeoutToEnsureRunning ()
    {
        var timeProvider = new ManualTimeProvider();

        var context = DaemonCommandExecutionContextTestFactory.CreateForRepositoryRoot(
            timeoutMilliseconds: 700,
            repositoryRoot: ProjectPathTestValues.TemporaryRepositoryRoot);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(), IpcUnityEditorObservationTestFactory.Create()),
        };
        var pluginVerifier = new RecordingUnityPluginVerifier
        {
            Handler = (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return ValueTask.FromResult(UnityPluginVerificationResult.Success());
            },
        };
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway, pluginVerifier, timeProvider: timeProvider);

        var result = await service.StartAsync(
            projectPath: ProjectPathTestValues.IndependentUnityProject,
            timeoutMilliseconds: 700,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonCommandExecutionContextResolverAssert.ResolvedFor(
            resolver,
            UcliCommandIds.DaemonStart,
            expectedProjectPath: ProjectPathTestValues.IndependentUnityProject,
            expectedTimeoutMilliseconds: 700);
        DaemonProjectLifecycleGatewayAssert.EnsureRunningRequestedWithExactTimeout(
            supervisorProjectGateway,
            context.Context.UnityProject,
            TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenProgressSinkConsumesTime_DoesNotConsumeTimeoutBudget ()
    {
        var timeProvider = new ManualTimeProvider();

        var context = DaemonCommandExecutionContextTestFactory.CreateForRepositoryRoot(
            timeoutMilliseconds: 700,
            repositoryRoot: ProjectPathTestValues.TemporaryRepositoryRoot);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(), IpcUnityEditorObservationTestFactory.Create()),
        };
        var pluginVerifier = new RecordingUnityPluginVerifier
        {
            Handler = (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return ValueTask.FromResult(UnityPluginVerificationResult.Success());
            },
        };
        var progressSink = new CollectingCommandProgressSink(() => timeProvider.Advance(TimeSpan.FromMilliseconds(250)));
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway, pluginVerifier, timeProvider: timeProvider);

        var result = await service.StartAsync(
            projectPath: ProjectPathTestValues.IndependentUnityProject,
            timeoutMilliseconds: 700,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink: progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonProjectLifecycleGatewayAssert.EnsureRunningRequestedWithExactTimeout(
            supervisorProjectGateway,
            context.Context.UnityProject,
            TimeSpan.FromMilliseconds(500));
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed));
    }
}
