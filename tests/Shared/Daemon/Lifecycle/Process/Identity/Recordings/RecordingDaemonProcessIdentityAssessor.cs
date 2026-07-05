namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
{
    private readonly List<Invocation> invocations = [];

    public RecordingDaemonProcessIdentityAssessor ()
        : this(new DaemonProcessIdentityAssessment(
            DaemonProcessIdentityAssessmentStatus.NotRunning,
            ObservedStartTimeUtc: null,
            Error: null))
    {
    }

    public RecordingDaemonProcessIdentityAssessor (DaemonProcessIdentityAssessmentStatus status)
        : this(new DaemonProcessIdentityAssessment(
            status,
            ObservedStartTimeUtc: null,
            Error: null))
    {
    }

    public RecordingDaemonProcessIdentityAssessor (DaemonProcessIdentityAssessment assessment)
    {
        Assessment = assessment;
    }

    public DaemonProcessIdentityAssessment Assessment { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public static RecordingDaemonProcessIdentityAssessor MatchingLiveProcess (
        DateTimeOffset? observedStartTimeUtc = null)
    {
        return new RecordingDaemonProcessIdentityAssessor(new DaemonProcessIdentityAssessment(
            DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
            observedStartTimeUtc ?? DateTimeOffset.UtcNow,
            Error: null));
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
}
