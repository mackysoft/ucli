namespace MackySoft.Ucli.Execution;

/// <summary> Represents one structured contract error emitted from mode decision. </summary>
/// <param name="Code"> The machine-readable error code. </param>
/// <param name="Message"> The human-readable error message. </param>
internal sealed record UnityExecutionModeDecisionContractError (
    string Code,
    string Message);