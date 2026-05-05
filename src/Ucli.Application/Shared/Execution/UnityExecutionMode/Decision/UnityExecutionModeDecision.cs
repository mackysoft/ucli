namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Represents one resolved mode decision ready for command execution. </summary>
/// <param name="RequestedMode"> The requested mode parsed from CLI input. </param>
/// <param name="DaemonRunning"> Whether daemon reachability probe succeeded. </param>
/// <param name="Target"> The selected execution target. </param>
/// <param name="Timeout"> The resolved timeout applied to probing and request execution. </param>
internal sealed record UnityExecutionModeDecision (
    UnityExecutionMode RequestedMode,
    bool DaemonRunning,
    UnityExecutionTarget Target,
    TimeSpan Timeout);
