namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
{
    private readonly string reason;

    public UnexpectedDaemonProcessIdentityAssessor (string reason)
    {
        this.reason = reason;
    }

    public DaemonProcessIdentityAssessment AssessByProcessId (
        int processId,
        DateTimeOffset? expectedProcessStartedAtUtc)
    {
        throw new InvalidOperationException(reason);
    }
}
