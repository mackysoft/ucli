using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Contracts.Storage;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonListQueryServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonListQueryServiceProbeFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeTimesOut_ReturnsProbeTimeoutItem ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fp-current",
            endpointAddress: "endpoint-timeout",
            processId: 2100);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new RecordingDaemonDiagnosisStore(),
            CreateThrowingPingClient(new TimeoutException("probe timed out")),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Error, item.State);
        Assert.Equal(DaemonListItemReason.ProbeTimeout, item.Reason);
        Assert.Equal(2100, item.ProcessId);
        Assert.Equal("endpoint-timeout", item.EndpointAddress);
        Assert.Null(item.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeClassifiesNotRunningAndPersistedDiagnosisMatches_ReturnsStaleItemWithDiagnosis ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fp-current",
            endpointAddress: "endpoint-stale",
            processId: 2200);
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReasonValues.ShutdownRequested);
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            OnRead = (_, _) => DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            diagnosisStore,
            CreateThrowingPingClient(new SocketException((int)SocketError.ConnectionRefused)),
            new StubDaemonReachabilityClassifier(static exception => exception is SocketException));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Stale, item.State);
        Assert.Equal(DaemonListItemReason.StaleSession, item.Reason);
        Assert.Equal(2200, item.ProcessId);
        Assert.NotNull(item.Diagnosis);
        Assert.Equal(diagnosis.Reason, item.Diagnosis!.Reason);
        Assert.Equal(diagnosis.Message, item.Diagnosis.Message);
        Assert.Equal(diagnosis.ReportedBy, item.Diagnosis.ReportedBy);
        Assert.Equal(diagnosis.IsInferred, item.Diagnosis.IsInferred);
        Assert.Equal(diagnosis.UpdatedAtUtc, item.Diagnosis.UpdatedAtUtc);
        Assert.Equal(diagnosis.ProcessId, item.Diagnosis.ProcessId);
        Assert.Equal(diagnosis.ProcessStartedAtUtc, item.Diagnosis.ProcessStartedAtUtc);
        Assert.Equal(diagnosis.UnityLogPath, item.Diagnosis.UnityLogPath);
        Assert.Equal(diagnosis.StartupPhase, item.Diagnosis.StartupPhase);
        Assert.Equal(diagnosis.ActionRequired, item.Diagnosis.ActionRequired);
        Assert.NotNull(item.Diagnosis.PrimaryDiagnostic);
        Assert.Equal(diagnosis.PrimaryDiagnostic!.Kind, item.Diagnosis.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeClassifiesNotRunningWithoutPersistedDiagnosis_ReturnsExternalTerminationDiagnosis ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fp-current",
            endpointAddress: "endpoint-stale",
            processId: int.MaxValue);
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            diagnosisStore,
            CreateThrowingPingClient(new SocketException((int)SocketError.ConnectionRefused)),
            new StubDaemonReachabilityClassifier(static exception => exception is SocketException));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Stale, item.State);
        Assert.Equal(DaemonListItemReason.StaleSession, item.Reason);
        Assert.NotNull(item.Diagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.ExternalTerminationSuspected, item.Diagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisReportedByValues.Cli, item.Diagnosis.ReportedBy);
        Assert.True(item.Diagnosis.IsInferred);
        Assert.Equal(session.ProcessId, item.Diagnosis.ProcessId);
        DaemonDiagnosisStoreAssert.WrittenOnceWithReason(
            diagnosisStore,
            DaemonDiagnosisReasonValues.ExternalTerminationSuspected);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeFailsUnexpectedly_ReturnsProbeFailedItem ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fp-current",
            endpointAddress: "endpoint-failed",
            processId: 2300);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new RecordingDaemonDiagnosisStore(),
            CreateThrowingPingClient(new InvalidOperationException("boom")),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Error, item.State);
        Assert.Equal(DaemonListItemReason.ProbeFailed, item.Reason);
        Assert.Equal(2300, item.ProcessId);
        Assert.Null(item.Diagnosis);
    }
}
