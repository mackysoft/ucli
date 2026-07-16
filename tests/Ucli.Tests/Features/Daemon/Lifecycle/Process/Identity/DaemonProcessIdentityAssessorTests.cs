namespace MackySoft.Ucli.Tests.Daemon;

using System.Diagnostics;
using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonProcessIdentityAssessorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Assessment_WhenObservedStartTimeIsNotUtc_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => DaemonProcessIdentityAssessment.MatchingLiveProcess(
            new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.FromHours(9))));

        Assert.Equal("observedStartTimeUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Assessment_WhenErrorStateHasNoError_ThrowsArgumentNullException ()
    {
        var differentProcessException = Assert.Throws<ArgumentNullException>(
            () => DaemonProcessIdentityAssessment.DifferentProcess(DateTimeOffset.UtcNow, null!));
        var uncertainException = Assert.Throws<ArgumentNullException>(
            () => DaemonProcessIdentityAssessment.Uncertain(observedStartTimeUtc: null, null!));

        Assert.Equal("error", differentProcessException.ParamName);
        Assert.Equal("error", uncertainException.ParamName);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void AssessByProcessId_WhenProcessDoesNotExist_ReturnsNotRunning ()
    {
        var assessor = new DaemonProcessIdentityAssessor();

        var result = assessor.AssessByProcessId(int.MaxValue, DateTimeOffset.UtcNow);

        Assert.Equal(DaemonProcessIdentityAssessmentStatus.NotRunning, result.Status);
        Assert.Null(result.Error);
        Assert.Null(result.ObservedStartTimeUtc);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void AssessByProcessId_WhenProcessMatches_ReturnsMatchingLiveProcess ()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var assessor = new DaemonProcessIdentityAssessor();

        var result = assessor.AssessByProcessId(
            currentProcess.Id,
            currentProcess.StartTime.ToUniversalTime());

        Assert.Equal(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess, result.Status);
        Assert.Null(result.Error);
        Assert.NotNull(result.ObservedStartTimeUtc);
    }

    [Fact]
    [Trait("Size", "Medium")]
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

    [Fact]
    [Trait("Size", "Medium")]
    public void AssessProcess_WhenExpectedProcessStartTimeIsMissing_ReturnsUncertain ()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var assessor = new DaemonProcessIdentityAssessor();

        var result = assessor.AssessProcess(
            currentProcess,
            currentProcess.Id,
            expectedProcessStartedAtUtc: null);

        Assert.Equal(DaemonProcessIdentityAssessmentStatus.Uncertain, result.Status);
        Assert.NotNull(result.ObservedStartTimeUtc);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("processStartedAtUtc", error.Message, StringComparison.Ordinal);
    }
}
