namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Implements conversion from daemon diagnosis domain model to daemon command payload model. </summary>
internal sealed class DaemonDiagnosisOutputMapper : IDaemonDiagnosisOutputMapper
{
    /// <summary> Converts one daemon diagnosis domain model to daemon command payload model. </summary>
    /// <param name="diagnosis"> The daemon diagnosis domain model. </param>
    /// <returns> The daemon command payload model. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="diagnosis" /> is <see langword="null" />. </exception>
    public DaemonDiagnosisOutput ToOutput (DaemonDiagnosis diagnosis)
    {
        ArgumentNullException.ThrowIfNull(diagnosis);

        return new DaemonDiagnosisOutput(
            Reason: diagnosis.Reason,
            Message: diagnosis.Message,
            ReportedBy: diagnosis.ReportedBy,
            IsInferred: diagnosis.IsInferred,
            UpdatedAtUtc: diagnosis.UpdatedAtUtc,
            ProcessId: diagnosis.ProcessId);
    }
}