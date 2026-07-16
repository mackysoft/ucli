using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
{
    private readonly List<Invocation> invocations = [];

    public RecordingDaemonProcessIdentityAssessor ()
        : this(DaemonProcessIdentityAssessment.NotRunning())
    {
    }

    public RecordingDaemonProcessIdentityAssessor (DaemonProcessIdentityAssessmentStatus status)
        : this(CreateAssessment(status))
    {
    }

    public RecordingDaemonProcessIdentityAssessor (DaemonProcessIdentityAssessment assessment)
    {
        Assessment = assessment;
    }

    public DaemonProcessIdentityAssessment Assessment { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public static RecordingDaemonProcessIdentityAssessor MatchingLiveProcess (
        DateTimeOffset observedStartTimeUtc)
    {
        return new RecordingDaemonProcessIdentityAssessor(
            DaemonProcessIdentityAssessment.MatchingLiveProcess(observedStartTimeUtc));
    }

    public DaemonProcessIdentityAssessment AssessByProcessId (
        int processId,
        DateTimeOffset? expectedProcessStartedAtUtc)
    {
        invocations.Add(new Invocation(processId, expectedProcessStartedAtUtc));
        return Assessment;
    }

    internal readonly record struct Invocation (
        int ProcessId,
        DateTimeOffset? ExpectedProcessStartedAtUtc);

    private static DaemonProcessIdentityAssessment CreateAssessment (DaemonProcessIdentityAssessmentStatus status)
    {
        return status switch
        {
            DaemonProcessIdentityAssessmentStatus.NotRunning => DaemonProcessIdentityAssessment.NotRunning(),
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess =>
                DaemonProcessIdentityAssessment.MatchingLiveProcess(DateTimeOffset.UtcNow),
            DaemonProcessIdentityAssessmentStatus.DifferentProcess =>
                DaemonProcessIdentityAssessment.DifferentProcess(
                    DateTimeOffset.UtcNow,
                    ExecutionError.InternalError("The test process differs from the expected process.")),
            DaemonProcessIdentityAssessmentStatus.Uncertain =>
                DaemonProcessIdentityAssessment.Uncertain(
                    observedStartTimeUtc: null,
                    ExecutionError.InternalError("The test process identity is uncertain.")),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Process identity assessment status must be defined."),
        };
    }
}
