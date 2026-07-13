using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Creates daemon session token values for persisted daemon sessions. </summary>
internal interface IDaemonSessionTokenGenerator
{
    /// <summary> Creates one daemon session token. </summary>
    /// <returns> The created daemon session token value. </returns>
    IpcSessionToken Create ();
}
