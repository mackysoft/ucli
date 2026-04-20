namespace MackySoft.Ucli.Features.Daemon.UseCases.Common;

/// <summary> Converts daemon session domain model to daemon command session payload model. </summary>
internal interface IDaemonSessionOutputMapper
{
    /// <summary> Converts one daemon session domain model to daemon command session payload model. </summary>
    /// <param name="session"> The daemon session domain model. </param>
    /// <returns> The daemon command session payload model. </returns>
    DaemonSessionOutput ToOutput (DaemonSession session);
}