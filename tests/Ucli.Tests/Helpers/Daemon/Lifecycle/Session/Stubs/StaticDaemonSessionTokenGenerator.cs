using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class StaticDaemonSessionTokenGenerator : IDaemonSessionTokenGenerator
{
    public StaticDaemonSessionTokenGenerator (string sessionToken = "new-session-token")
    {
        SessionToken = IpcSessionTokenTestFactory.Create(sessionToken);
    }

    public IpcSessionToken SessionToken { get; }

    public IpcSessionToken Create ()
    {
        return SessionToken;
    }
}
