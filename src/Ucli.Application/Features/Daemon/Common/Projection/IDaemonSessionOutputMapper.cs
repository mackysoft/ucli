using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.Projection;

/// <summary> Converts daemon session domain model to daemon command session payload model. </summary>
internal interface IDaemonSessionOutputMapper
{
    /// <summary> Converts one daemon session domain model to daemon command session payload model. </summary>
    /// <param name="session"> The daemon session domain model. </param>
    /// <returns> The daemon command session payload model. </returns>
    DaemonSessionOutput ToOutput (DaemonSession session);
}
