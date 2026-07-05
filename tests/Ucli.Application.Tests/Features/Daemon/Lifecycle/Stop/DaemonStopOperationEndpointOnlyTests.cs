using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStopOperationTestSupport;
using static MackySoft.Ucli.Application.Tests.DaemonCleanupInvocationAssert;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStopOperationEndpointOnlyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenUserOwnedGuiSessionDoesNotAllowProcessShutdownAndShutdownSucceeds_InvalidatesEndpointOnly ()
    {
        var session = DaemonSessionTestFactory.Create(
            processId: 456,
            ownerKind: "user",
            canShutdownProcess: false,
            editorMode: "gui");
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-disallowed");
        var shutdownClient = new RecordingDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Success(),
        };
        var processTerminationService = new RecordingDaemonProcessTerminationService();
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = CreateOperation(
            sessionStore: CreateSessionStore(session),
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await operation.StopAsync(context, DefaultTimeout, CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.Null(result.Error);
        DaemonShutdownClientAssert.EndpointShutdownAttempted(shutdownClient, context, session);
        AssertSessionArtifactsInvalidatedWithoutProcessTermination(processTerminationService, artifactCleaner, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenEndpointOnlyGuiShutdownTimesOut_ReturnsFailureWithoutCleanup ()
    {
        var shutdownError = ExecutionError.Timeout("shutdown timed out");
        var session = DaemonSessionTestFactory.Create(
            processId: 456,
            ownerKind: "user",
            canShutdownProcess: false,
            editorMode: "gui");
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-endpoint-timeout");
        var shutdownClient = new RecordingDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.Failure(shutdownError),
        };
        var processTerminationService = new RecordingDaemonProcessTerminationService();
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = CreateOperation(
            sessionStore: CreateSessionStore(session),
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await operation.StopAsync(context, DefaultTimeout, CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        Assert.Equal(shutdownError, result.Error);
        DaemonShutdownClientAssert.EndpointShutdownAttempted(shutdownClient, context, session);
        AssertProcessTerminationAndArtifactCleanupSkipped(processTerminationService, artifactCleaner);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenCliOwnedGuiSessionDoesNotAllowProcessShutdownAndEndpointIsNotRunning_CleansUpEndpointOnly ()
    {
        var session = DaemonSessionTestFactory.Create(
            processId: 457,
            ownerKind: "cli",
            canShutdownProcess: false,
            editorMode: "gui");
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-cli-endpoint-only");
        var shutdownClient = new RecordingDaemonShutdownClient
        {
            NextResult = DaemonShutdownAttemptResult.NotRunning(),
        };
        var processTerminationService = new RecordingDaemonProcessTerminationService();
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = CreateOperation(
            sessionStore: CreateSessionStore(session),
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await operation.StopAsync(context, DefaultTimeout, CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.Null(result.Error);
        DaemonShutdownClientAssert.EndpointShutdownAttempted(shutdownClient, context, session);
        AssertSessionArtifactsInvalidatedWithoutProcessTermination(processTerminationService, artifactCleaner, context);
    }
}
