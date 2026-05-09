namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;

/// <summary> Assesses whether one operating-system process still matches expected daemon session identity. </summary>
internal interface IDaemonProcessIdentityAssessor
{
    /// <summary> Resolves one process by identifier and assesses whether it matches expected daemon identity. </summary>
    /// <param name="processId"> The process identifier to assess. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon session issued-at timestamp. </param>
    /// <returns> The process identity assessment result. </returns>
    DaemonProcessIdentityAssessment AssessByProcessId (
        int processId,
        DateTimeOffset expectedIssuedAtUtc);
}
