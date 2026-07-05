using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class StaticDaemonSessionTokenGenerator : IDaemonSessionTokenGenerator
{
    public StaticDaemonSessionTokenGenerator (string sessionToken = "new-session-token")
    {
        SessionToken = sessionToken;
    }

    public string SessionToken { get; }

    public string Create ()
    {
        return SessionToken;
    }
}
