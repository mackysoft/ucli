using DiagnosticsProcess = System.Diagnostics.Process;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

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

    /// <summary> Assesses one already-resolved process against expected daemon identity. </summary>
    /// <param name="process"> The already-resolved process instance. </param>
    /// <param name="processId"> The process identifier used for diagnostics. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon session issued-at timestamp. </param>
    /// <returns> The process identity assessment result. </returns>
    DaemonProcessIdentityAssessment AssessProcess (
        DiagnosticsProcess process,
        int processId,
        DateTimeOffset expectedIssuedAtUtc);
}