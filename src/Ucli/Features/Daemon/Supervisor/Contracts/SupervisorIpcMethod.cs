using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Contracts;

/// <summary> Defines the closed set of supervisor IPC methods. </summary>
internal enum SupervisorIpcMethod
{
    /// <summary> No supervisor IPC method. </summary>
    [UcliContractLiteralIgnore]
    Unspecified = 0,

    /// <summary> Probes supervisor health. </summary>
    [UcliContractLiteral("supervisor.ping")]
    Ping,

    /// <summary> Ensures one Unity daemon is running. </summary>
    [UcliContractLiteral("supervisor.ensureRunning")]
    EnsureRunning,

    /// <summary> Stops one Unity daemon. </summary>
    [UcliContractLiteral("supervisor.stopProject")]
    StopProject,
}
