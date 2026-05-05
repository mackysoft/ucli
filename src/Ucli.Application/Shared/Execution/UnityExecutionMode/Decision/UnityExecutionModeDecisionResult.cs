using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Represents the result of execution mode decision. </summary>
/// <param name="Decision"> The resolved execution decision on success; otherwise <see langword="null" />. </param>
/// <param name="ContractError"> The mode-contract error when the requested mode is currently forbidden; otherwise <see langword="null" />. </param>
/// <param name="Error"> The infrastructure error when decision fails unexpectedly; otherwise <see langword="null" />. </param>
internal sealed record UnityExecutionModeDecisionResult (
    UnityExecutionModeDecision? Decision,
    UnityExecutionModeDecisionContractError? ContractError,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether mode decision succeeded. </summary>
    public bool IsSuccess => Decision is not null && ContractError is null && Error is null;

    /// <summary> Gets a value indicating whether mode decision failed with a contract error. </summary>
    public bool HasContractError => Decision is null && ContractError is not null && Error is null;

    /// <summary> Creates a successful mode decision result. </summary>
    /// <param name="decision"> The resolved mode decision. </param>
    /// <returns> The successful decision result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="decision" /> is <see langword="null" />. </exception>
    public static UnityExecutionModeDecisionResult Success (UnityExecutionModeDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new UnityExecutionModeDecisionResult(decision, null, null);
    }

    /// <summary> Creates a mode decision result that failed with a contract error. </summary>
    /// <param name="contractError"> The contract error. </param>
    /// <returns> The contract-failure decision result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contractError" /> is <see langword="null" />. </exception>
    public static UnityExecutionModeDecisionResult ContractFailure (UnityExecutionModeDecisionContractError contractError)
    {
        ArgumentNullException.ThrowIfNull(contractError);
        return new UnityExecutionModeDecisionResult(null, contractError, null);
    }

    /// <summary> Creates a failed mode decision result with an infrastructure error. </summary>
    /// <param name="error"> The infrastructure error. </param>
    /// <returns> The failed decision result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityExecutionModeDecisionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityExecutionModeDecisionResult(null, null, error);
    }
}
