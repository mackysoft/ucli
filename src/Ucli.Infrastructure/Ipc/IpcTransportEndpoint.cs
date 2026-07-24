using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary>
/// Carries one IPC transport contract with the guarded filesystem path required by a Unix-domain socket.
/// </summary>
internal sealed record IpcTransportEndpoint
{
    private IpcTransportEndpoint (
        IpcEndpoint contract,
        AbsolutePath? unixSocketPath)
    {
        Contract = contract;
        UnixSocketPath = unixSocketPath;
    }

    /// <summary>
    /// Gets the transport contract retained for wire and persistence adapters and for the final Named Pipe adapter.
    /// </summary>
    public IpcEndpoint Contract { get; }

    /// <summary>
    /// Gets the guarded Unix-domain-socket path, or <see langword="null" /> for a Named Pipe endpoint.
    /// </summary>
    public AbsolutePath? UnixSocketPath { get; }

    /// <summary> Creates a Named Pipe endpoint without interpreting its logical address as a filesystem path. </summary>
    public static IpcTransportEndpoint FromNamedPipeAddress (string address)
    {
        return RetainGuardedValues(
            new IpcEndpoint(IpcTransportKind.NamedPipe, address),
            unixSocketPath: null);
    }

    /// <summary> Creates a Unix-domain-socket endpoint from an already guarded absolute path. </summary>
    public static IpcTransportEndpoint FromUnixSocketPath (AbsolutePath socketPath)
    {
        if (socketPath is null)
        {
            throw new ArgumentNullException(nameof(socketPath));
        }

        UnixSocketEndpointPathPolicy.EnsureSupported(socketPath);
        return RetainGuardedValues(
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, socketPath.Value),
            socketPath);
    }

    /// <summary>
    /// Converts a wire or persistence contract at its input boundary, parsing Unix socket text exactly once.
    /// </summary>
    public static IpcTransportEndpoint FromContract (IpcEndpoint contract)
    {
        if (contract is null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        return contract.TransportKind switch
        {
            IpcTransportKind.NamedPipe => RetainGuardedValues(
                contract,
                unixSocketPath: null),
            IpcTransportKind.UnixDomainSocket => FromUnixSocketContract(contract),
            _ => throw new ArgumentOutOfRangeException(
                nameof(contract),
                contract.TransportKind,
                "IPC endpoint transport kind is unsupported."),
        };
    }

    /// <summary> Attempts to get the guarded path required by a Unix-domain-socket operation. </summary>
    public bool TryGetUnixSocketPath ([NotNullWhen(true)] out AbsolutePath? socketPath)
    {
        socketPath = UnixSocketPath;
        return socketPath is not null;
    }

    /// <summary>
    /// Retains a validated endpoint contract and guarded path without repeating raw path or transport-policy validation.
    /// </summary>
    internal static IpcTransportEndpoint RetainGuardedValues (
        IpcEndpoint contract,
        AbsolutePath? unixSocketPath)
    {
        if (contract is null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        switch (contract.TransportKind)
        {
            case IpcTransportKind.NamedPipe when unixSocketPath is not null:
                throw new ArgumentException(
                    "Named Pipe endpoints must not retain a Unix-domain-socket path.",
                    nameof(unixSocketPath));

            case IpcTransportKind.UnixDomainSocket when unixSocketPath is null:
                throw new ArgumentException(
                    "Unix-domain-socket endpoints must retain a guarded absolute path.",
                    nameof(unixSocketPath));

            case IpcTransportKind.UnixDomainSocket when !string.Equals(
                contract.Address,
                unixSocketPath!.Value,
                StringComparison.Ordinal):
                throw new ArgumentException(
                    "Unix-domain-socket endpoint contract and guarded path must identify the same normalized path.",
                    nameof(unixSocketPath));
        }

        return new IpcTransportEndpoint(contract, unixSocketPath);
    }

    private static IpcTransportEndpoint FromUnixSocketContract (IpcEndpoint contract)
    {
        var socketPath = UnixSocketEndpointPathPolicy.Parse(contract.Address);
        var normalizedContract = string.Equals(
                contract.Address,
                socketPath.Value,
                StringComparison.Ordinal)
            ? contract
            : new IpcEndpoint(
                IpcTransportKind.UnixDomainSocket,
                socketPath.Value);
        return RetainGuardedValues(normalizedContract, socketPath);
    }
}
