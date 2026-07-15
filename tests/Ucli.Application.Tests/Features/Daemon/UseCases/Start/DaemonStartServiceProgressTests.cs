using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartServiceProgressTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WithProgressSink_EmitsHostVisibleProgressInOrder ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create();
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            EnsureRunningHandler = async (_, _, _, _, progressObserver, supervisorProgressSink, cancellationToken) =>
            {
                Assert.NotNull(progressObserver);
                Assert.NotNull(supervisorProgressSink);
                await progressObserver!.EmitSupervisorBootstrapStartedAsync(cancellationToken).ConfigureAwait(false);
                await progressObserver.EmitSupervisorBootstrapCompletedAsync(error: null, cancellationToken).ConfigureAwait(false);
                await progressObserver.EmitEnsureRunningStartedAsync(cancellationToken).ConfigureAwait(false);
                var startResult = DaemonStartResult.Started(session, IpcUnityEditorObservationTestFactory.Create());
                await progressObserver.EmitEnsureRunningCompletedAsync(startResult, cancellationToken).ConfigureAwait(false);
                return startResult;
            },
        };
        var progressSink = new CollectingCommandProgressSink();
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway);

        var result = await service.StartAsync(
            projectPath: null,
            timeoutMilliseconds: null,
            editorMode: DaemonEditorMode.Batchmode,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink: progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonProjectLifecycleGatewayAssert.EnsureRunningRequestedWithProgressSink(supervisorProjectGateway, progressSink);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed));
        DaemonStartProgressAssert.BatchmodeStartCompletedSuccessfully(
            progressSink,
            expectedProjectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
            expectedTimeoutMilliseconds: 1200);
    }
}
