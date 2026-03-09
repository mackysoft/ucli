namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Converts daemon diagnosis domain model to daemon command payload model. </summary>
internal interface IDaemonDiagnosisOutputMapper
{
    /// <summary> Converts one daemon diagnosis domain model to daemon command payload model. </summary>
    /// <param name="diagnosis"> The daemon diagnosis domain model. </param>
    /// <returns> The daemon command payload model. </returns>
    DaemonDiagnosisOutput ToOutput (DaemonDiagnosis diagnosis);
}