using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Contracts;

/// <summary> Defines the closed set of supervisor IPC methods. </summary>
internal enum SupervisorIpcMethod
{
    /// <summary> Probes supervisor health. </summary>
    [UcliContractLiteral("supervisor.ping")]
    Ping = 1,

    /// <summary> Ensures one Unity daemon is running. </summary>
    [UcliContractLiteral("supervisor.ensureRunning")]
    EnsureRunning,

    /// <summary> Stops one Unity daemon. </summary>
    [UcliContractLiteral("supervisor.stopProject")]
    StopProject,
}
