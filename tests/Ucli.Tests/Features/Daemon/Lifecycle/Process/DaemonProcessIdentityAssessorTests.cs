using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Diagnostics;
using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonProcessIdentityAssessorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AssessByProcessId_WhenProcessDoesNotExist_ReturnsNotRunning ()
    {
        var assessor = new DaemonProcessIdentityAssessor();

        var result = assessor.AssessByProcessId(int.MaxValue, DateTimeOffset.UtcNow);

        Assert.Equal(DaemonProcessIdentityAssessmentStatus.NotRunning, result.Status);
        Assert.Null(result.Error);
        Assert.Null(result.ObservedStartTimeUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssessByProcessId_WhenProcessMatches_ReturnsMatchingLiveProcess ()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var assessor = new DaemonProcessIdentityAssessor();

        var result = assessor.AssessByProcessId(
            currentProcess.Id,
            new DateTimeOffset(currentProcess.StartTime.ToUniversalTime()).AddSeconds(1));

        Assert.Equal(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess, result.Status);
        Assert.Null(result.Error);
        Assert.NotNull(result.ObservedStartTimeUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssessProcess_WhenProcessStartTimeDoesNotMatch_ReturnsDifferentProcessWithError ()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var assessor = new DaemonProcessIdentityAssessor();

        var result = assessor.AssessProcess(
            currentProcess,
            currentProcess.Id,
            DateTimeOffset.UtcNow.AddHours(1));

        Assert.Equal(DaemonProcessIdentityAssessmentStatus.DifferentProcess, result.Status);
        Assert.NotNull(result.ObservedStartTimeUtc);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("identity mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
