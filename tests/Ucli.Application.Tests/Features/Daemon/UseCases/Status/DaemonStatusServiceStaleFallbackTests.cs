using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStatusServiceStaleFallbackTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenPingInfoReadFallsBackToStale_ResolvesDiagnosisForSession ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2500);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create();
        var persistedDiagnosis = DaemonDiagnosisTestFactory.Create();
        var diagnosis = DaemonDiagnosisTestFactory.Create();
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(session, persistedDiagnosis));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new InvalidOperationException("daemon exited"));
        var reachabilityClassifier = new StubDaemonReachabilityClassifier(static _ => true);
        var diagnosisResolver = new RecordingDaemonSessionDiagnosisResolver
        {
            Diagnosis = diagnosis,
        };
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            reachabilityClassifier,
            diagnosisResolver);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Stale, output.DaemonStatus);
        DaemonServiceOutputAssert.SessionMatches(session, output.Session);
        DaemonServiceOutputAssert.DiagnosisMatches(diagnosis, output.Diagnosis);
        DaemonStatusServiceInvocationAssert.DaemonPingTelemetryRead(
            pingInfoClient,
            context,
            expectedTimeout: null,
            expectedSessionToken: session.SessionToken);
        DaemonStatusServiceInvocationAssert.StaleDiagnosisResolved(diagnosisResolver, context, session, persistedDiagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStaleFallbackBudgetIsAlreadyExpired_ReturnsTimeoutBeforeDiagnosisResolution ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 250);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create();
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(session));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new InvalidOperationException("daemon exited"))
        {
            OnPingAndRead = () => timeProvider.Advance(TimeSpan.FromMilliseconds(250)),
        };
        var diagnosisResolver = new RecordingDaemonSessionDiagnosisResolver();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => true),
            diagnosisResolver,
            timeProvider);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: 250, cancellationToken: CancellationToken.None);

        DaemonStatusServiceInvocationAssert.TimeoutReturnedBeforeStaleDiagnosisResolution(result, diagnosisResolver);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStaleFallbackDiagnosisResolutionTimesOut_ReturnsTimeoutFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 250);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create();
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(session));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new InvalidOperationException("daemon exited"));
        var diagnosisStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosisResolver = new RecordingDaemonSessionDiagnosisResolver
        {
            Handler = async (_, _, _, cancellationToken) =>
            {
                diagnosisStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new System.Diagnostics.UnreachableException();
            },
        };
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => true),
            diagnosisResolver,
            timeProvider);

        var resultTask = service.GetStatusAsync(projectPath: null, timeoutMilliseconds: 250, cancellationToken: CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(diagnosisStarted.Task, "Daemon status stale diagnosis start", TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(250));

        var result = await TestAwaiter.WaitAsync(
            resultTask,
            "Daemon status stale diagnosis timeout result",
            TimeSpan.FromSeconds(5));

        DaemonStatusServiceInvocationAssert.StaleDiagnosisResolutionTimedOut(
            result,
            diagnosisResolver,
            context,
            session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStaleFallbackDiagnosisResolutionThrowsUnexpectedException_ReturnsInternalError ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 250);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create();
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(session));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new InvalidOperationException("daemon exited"));
        var diagnosisResolver = new RecordingDaemonSessionDiagnosisResolver
        {
            Handler = static (_, _, _, _) => ValueTask.FromException<DaemonDiagnosis?>(new InvalidOperationException("diagnosis store failed")),
        };
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => true),
            diagnosisResolver);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: 250, cancellationToken: CancellationToken.None);

        DaemonStatusServiceInvocationAssert.StaleDiagnosisResolutionFailed(
            result,
            diagnosisResolver,
            context,
            session);
    }
}
