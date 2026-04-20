namespace MackySoft.Ucli.Tests.Daemon;

using System.Diagnostics;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Shared.Foundation;

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