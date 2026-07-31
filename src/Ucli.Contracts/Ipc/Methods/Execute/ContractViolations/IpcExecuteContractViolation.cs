using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Json.Metadata;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one runtime operation-result violation against published assurance facts. </summary>
/// <param name="InstancePath"> The RFC 6901 path of the operation result associated with the violation. </param>
/// <param name="Operation"> The operation name whose runtime result violated its contract. </param>
/// <param name="ExpectedFact"> The assurance fact expected by the operation metadata. </param>
/// <param name="ObservedResult"> The observed result fact that contradicted the expected fact. </param>
/// <param name="ApplicationState"> The application state used to decide retry safety. </param>
public sealed record IpcExecuteContractViolation
{
    /// <summary> Initializes one contract violation with a specified application state. </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="ApplicationState" /> is not supported by the operation contract.
    /// </exception>
    [JsonConstructor]
    public IpcExecuteContractViolation (
        string InstancePath,
        string Operation,
        string ExpectedFact,
        string ObservedResult,
        ExecutionApplicationState ApplicationState)
    {
        this.InstancePath = IpcInstancePathContract.Require(InstancePath, nameof(InstancePath));
        this.Operation = ContractArgumentGuard.RequireValue(Operation, nameof(Operation));
        this.ExpectedFact = ContractArgumentGuard.RequireValue(ExpectedFact, nameof(ExpectedFact));
        this.ObservedResult = ContractArgumentGuard.RequireValue(ObservedResult, nameof(ObservedResult));
        this.ApplicationState =
            ExecutionApplicationStateSemantics.RequireOperationState(
                ApplicationState,
                nameof(ApplicationState));
    }

    public string InstancePath { get; }

    public string Operation { get; }

    public string ExpectedFact { get; }

    public string ObservedResult { get; }

    [UcliOperationApplicationState]
    public ExecutionApplicationState ApplicationState { get; }
}
