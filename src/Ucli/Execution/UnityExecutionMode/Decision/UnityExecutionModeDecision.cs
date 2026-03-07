using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Execution;

/// <summary> Represents one resolved mode decision ready for command execution. </summary>
/// <param name="RequestedMode"> The requested mode parsed from CLI input. </param>
/// <param name="DaemonRunning"> Whether daemon reachability probe succeeded. </param>
/// <param name="Target"> The selected execution target. </param>
internal sealed record UnityExecutionModeDecision (
    UnityExecutionMode RequestedMode,
    bool DaemonRunning,
    UnityExecutionTarget Target);