using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one runtime operation-result violation against published assurance facts. </summary>
/// <param name="OpId"> The public operation identifier associated with the violation. </param>
/// <param name="Operation"> The operation name whose runtime result violated its contract. </param>
/// <param name="ExpectedFact"> The assurance fact expected by the operation metadata. </param>
/// <param name="ObservedResult"> The observed result fact that contradicted the expected fact. </param>
/// <param name="ApplicationState"> The application state used to decide retry safety. </param>
public sealed record IpcExecuteContractViolation
{
    /// <summary> Initializes one contract violation with a specified application state. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="ApplicationState" /> is not defined by the contract. </exception>
    [JsonConstructor]
    public IpcExecuteContractViolation (
        IpcExecuteStepId OpId,
        string Operation,
        string ExpectedFact,
        string ObservedResult,
        IpcApplicationState ApplicationState)
    {
        if (!ContractLiteralCodec.IsDefined(ApplicationState))
        {
            throw new ArgumentOutOfRangeException(nameof(ApplicationState), ApplicationState, "Application state must be specified.");
        }

        this.OpId = OpId ?? throw new ArgumentNullException(nameof(OpId));
        this.Operation = ContractArgumentGuard.RequireValue(Operation, nameof(Operation));
        this.ExpectedFact = ContractArgumentGuard.RequireValue(ExpectedFact, nameof(ExpectedFact));
        this.ObservedResult = ContractArgumentGuard.RequireValue(ObservedResult, nameof(ObservedResult));
        this.ApplicationState = ApplicationState;
    }

    public IpcExecuteStepId OpId { get; }

    public string Operation { get; }

    public string ExpectedFact { get; }

    public string ObservedResult { get; }

    public IpcApplicationState ApplicationState { get; }
}
