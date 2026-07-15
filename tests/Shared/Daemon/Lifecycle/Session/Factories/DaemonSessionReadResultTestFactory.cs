using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonSessionReadResultTestFactory
{
    public static DaemonSessionReadResult Found (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return DaemonSessionReadResult.Found(
            session,
            DaemonSessionArtifactIdentity.Create(
                System.Text.Encoding.UTF8.GetBytes("synthetic-test-session-artifact")));
    }

    public static DaemonSessionReadResult FoundForToken (
        string sessionToken,
        IpcTransportKind endpointTransportKind = IpcTransportKind.UnixDomainSocket,
        string endpointAddress = "/tmp/ucli-session.sock")
    {
        return Found(DaemonSessionTestFactory.CreateForToken(
            sessionToken,
            endpointTransportKind,
            endpointAddress));
    }

    public static DaemonSessionReadResult Invalid (
        DaemonInvalidSessionEvidence? evidence = null,
        DaemonSessionArtifactIdentity? artifactIdentity = null)
    {
        return DaemonSessionReadResult.Invalid(
            ExecutionError.InvalidArgument("Synthetic invalid daemon session."),
            evidence,
            artifactIdentity ?? DaemonSessionArtifactIdentity.Create(
                System.Text.Encoding.UTF8.GetBytes("synthetic-invalid-test-session-artifact")));
    }
}
