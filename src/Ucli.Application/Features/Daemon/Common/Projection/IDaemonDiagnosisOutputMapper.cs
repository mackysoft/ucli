using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.Projection;

/// <summary> Converts daemon diagnosis domain model to daemon command payload model. </summary>
internal interface IDaemonDiagnosisOutputMapper
{
    /// <summary> Converts one daemon diagnosis domain model to daemon command payload model. </summary>
    /// <param name="diagnosis"> The daemon diagnosis domain model. </param>
    /// <returns> The daemon command payload model. </returns>
    DaemonDiagnosisOutput ToOutput (DaemonDiagnosis diagnosis);
}
