using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonSessionReadResultTestFactory
{
    public static DaemonSessionReadResult Found (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return DaemonSessionReadResult.Found(
            session,
            DaemonSessionArtifactIdentity.Create("synthetic-test-session-artifact"));
    }

    public static DaemonSessionReadResult Invalid (
        DaemonInvalidSessionEvidence? evidence = null,
        DaemonSessionArtifactIdentity? artifactIdentity = null)
    {
        return DaemonSessionReadResult.Invalid(
            ExecutionError.InvalidArgument("Synthetic invalid daemon session."),
            evidence,
            artifactIdentity ?? DaemonSessionArtifactIdentity.Create("synthetic-invalid-test-session-artifact"));
    }
}
