using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Represents validated runtime metadata for one worktree-local supervisor instance. </summary>
internal sealed record SupervisorInstanceManifest
{
    /// <summary> Initializes one validated supervisor runtime manifest. </summary>
    public SupervisorInstanceManifest (
        int processId,
        IpcSessionToken sessionToken,
        IpcEndpoint endpoint,
        DateTimeOffset issuedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        ArgumentNullException.ThrowIfNull(sessionToken);
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!Enum.IsDefined(endpoint.TransportKind))
        {
            throw new ArgumentException("Supervisor endpoint transport kind must be defined.", nameof(endpoint));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.Address);
        if (issuedAtUtc == default)
        {
            throw new ArgumentException("Supervisor issue timestamp must not be the default value.", nameof(issuedAtUtc));
        }

        ProcessId = processId;
        SessionToken = sessionToken;
        Endpoint = endpoint;
        IssuedAtUtc = issuedAtUtc;
    }

    /// <summary> Gets the supervisor process identifier. </summary>
    public int ProcessId { get; }

    /// <summary> Gets the canonical supervisor session token. </summary>
    public IpcSessionToken SessionToken { get; }

    /// <summary> Gets the validated runtime endpoint. </summary>
    public IpcEndpoint Endpoint { get; }

    /// <summary> Gets the UTC timestamp when this supervisor generation was issued. </summary>
    public DateTimeOffset IssuedAtUtc { get; }
}
