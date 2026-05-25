using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Represents the IPC connection values persisted in one daemon session. </summary>
/// <param name="SessionToken"> The daemon session token used for IPC authorization. </param>
/// <param name="Endpoint"> The daemon IPC endpoint registered by the running Unity process. </param>
internal sealed record DaemonSessionConnection (
    string SessionToken,
    IpcEndpoint Endpoint);
