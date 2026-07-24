
namespace MackySoft.Ucli.Features.Daemon.Supervisor.Contracts;

/// <summary> Defines the closed set of supervisor IPC methods. </summary>
[VocabularyDefinition]
internal enum SupervisorIpcMethod
{
    /// <summary> Probes supervisor health. </summary>
    [VocabularyText("supervisor.ping")]
    Ping = 1,

    /// <summary> Ensures one Unity daemon is running. </summary>
    [VocabularyText("supervisor.ensureRunning")]
    EnsureRunning,

    /// <summary> Stops one Unity daemon. </summary>
    [VocabularyText("supervisor.stopProject")]
    StopProject,
}
