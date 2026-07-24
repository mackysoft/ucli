using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Common.Ipc;

/// <summary>
/// Adapts one validated daemon session to the guarded endpoint consumed by runtime transports.
/// </summary>
internal static class DaemonSessionIpcTransportEndpointAdapter
{
    private static readonly ConditionalWeakTable<DaemonSession, IpcTransportEndpoint> RuntimeEndpoints = new();

    /// <summary>
    /// Converts one persisted or wire session contract to a runtime session and binds its guarded endpoint.
    /// </summary>
    /// <param name="contract"> The raw daemon session contract. </param>
    /// <param name="expectedProjectFingerprint"> The project fingerprint expected by the consuming boundary. </param>
    /// <param name="sourceDescription"> The contract source included in diagnostic errors. </param>
    /// <param name="session"> The validated runtime session when successful. </param>
    /// <param name="error"> The validation error when unsuccessful. </param>
    /// <returns> <see langword="true" /> when the contract represents a valid session; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        DaemonSessionJsonContract contract,
        ProjectFingerprint expectedProjectFingerprint,
        string sourceDescription,
        [NotNullWhen(true)] out DaemonSession? session,
        [NotNullWhen(false)] out ExecutionError? error)
    {
        IpcTransportEndpoint? runtimeEndpoint = null;
        var isValid = DaemonSessionContractMapper.TryCreate(
            contract,
            expectedProjectFingerprint,
            sourceDescription,
            CreateEndpointBinding,
            out var mappedSession,
            out var validationError);
        if (!isValid)
        {
            session = null;
            error = validationError
                ?? throw new InvalidOperationException(
                    "A failed daemon session mapping must return a validation error.");
            return false;
        }

        session = mappedSession
            ?? throw new InvalidOperationException(
                "A successful daemon session mapping must return a session.");
        error = null;
        Bind(
            session,
            runtimeEndpoint
                ?? throw new InvalidOperationException(
                    "A successful daemon session mapping must create a runtime endpoint."));
        return true;

        DaemonSessionEndpointBinding CreateEndpointBinding (
            IpcTransportKind transportKind,
            string address)
        {
            runtimeEndpoint = IpcTransportEndpoint.FromContract(
                new IpcEndpoint(transportKind, address));
            return new DaemonSessionEndpointBinding(
                runtimeEndpoint.Contract,
                runtimeEndpoint.UnixSocketPath);
        }
    }

    /// <summary> Retains the session's guarded Unix socket path without reparsing its wire address. </summary>
    /// <param name="session"> The validated daemon session to adapt. </param>
    /// <returns> The guarded runtime endpoint for the session transport. </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="session" /> is <see langword="null" />.
    /// </exception>
    public static IpcTransportEndpoint Adapt (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return RuntimeEndpoints.GetValue(session, CreateEndpointFromGuardedValues);
    }

    /// <summary>
    /// Associates a newly created session with the already validated endpoint that produced its contract.
    /// </summary>
    /// <param name="session"> The newly created daemon session. </param>
    /// <param name="endpoint"> The guarded endpoint used to create the session. </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="session" /> or <paramref name="endpoint" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the session contract and guarded endpoint do not identify the same transport address.
    /// </exception>
    public static void Bind (
        DaemonSession session,
        IpcTransportEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(endpoint);
        if (session.EndpointContract != endpoint.Contract
            || session.UnixSocketEndpointPath != endpoint.UnixSocketPath)
        {
            throw new ArgumentException(
                "Daemon session and runtime endpoint must identify the same transport address.",
                nameof(endpoint));
        }

        RuntimeEndpoints.Add(session, endpoint);
    }

    private static IpcTransportEndpoint CreateEndpointFromGuardedValues (DaemonSession session)
    {
        return IpcTransportEndpoint.RetainGuardedValues(
            session.EndpointContract,
            session.UnixSocketEndpointPath);
    }
}
