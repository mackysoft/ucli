using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Creates daemon IPC connection values from persisted daemon session metadata. </summary>
internal static class DaemonSessionConnectionFactory
{
    /// <summary> Tries to create one IPC connection descriptor from a daemon session. </summary>
    /// <param name="session"> The persisted daemon session metadata. </param>
    /// <param name="connection"> The created connection descriptor when successful; otherwise <see langword="null" />. </param>
    /// <param name="error"> The structured validation error when creation fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when connection creation succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        DaemonSession session,
        [NotNullWhen(true)] out DaemonSessionConnection? connection,
        [NotNullWhen(false)] out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(session.SessionToken))
        {
            connection = null;
            error = ExecutionError.InvalidArgument("Daemon session token is missing.");
            return false;
        }

        if (!ContractLiteralCodec.TryParse<IpcTransportKind>(session.EndpointTransportKind, out var transportKind))
        {
            connection = null;
            error = ExecutionError.InvalidArgument(
                $"Daemon session endpointTransportKind is invalid: {session.EndpointTransportKind}.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(session.EndpointAddress))
        {
            connection = null;
            error = ExecutionError.InvalidArgument("Daemon session endpointAddress is missing.");
            return false;
        }

        connection = new DaemonSessionConnection(
            session.SessionToken,
            new IpcEndpoint(transportKind, session.EndpointAddress));
        error = null;
        return true;
    }
}
