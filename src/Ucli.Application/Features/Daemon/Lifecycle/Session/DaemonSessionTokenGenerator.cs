using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Implements cryptographically random daemon session token generation. </summary>
internal sealed class DaemonSessionTokenGenerator : IDaemonSessionTokenGenerator
{
    /// <summary> Creates one daemon session token. </summary>
    /// <returns> The created daemon session token value. </returns>
    public IpcSessionToken Create ()
    {
        return IpcSessionToken.CreateRandom();
    }
}
