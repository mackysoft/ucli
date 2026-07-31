using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Json.Metadata;

namespace MackySoft.Ucli.Application.Shared.Execution.Results;

/// <summary> Represents one operation-result violation against published assurance facts. </summary>
internal sealed record OperationExecutionContractViolation
{
    /// <summary> Initializes one validated operation-result contract violation. </summary>
    /// <param name="InstancePath"> The RFC 6901 path of the operation result associated with the violation. </param>
    /// <param name="Operation"> The operation name whose runtime result violated its contract. </param>
    /// <param name="ExpectedFact"> The assurance fact expected by the operation metadata. </param>
    /// <param name="ObservedResult"> The observed result fact that contradicted the expected fact. </param>
    /// <param name="ApplicationState"> The four-state operation application state used to decide retry safety. </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="ApplicationState" /> is undefined or uses the lifecycle-only partial-application state.
    /// </exception>
    internal OperationExecutionContractViolation (
        string InstancePath,
        string Operation,
        string ExpectedFact,
        string ObservedResult,
        ExecutionApplicationState ApplicationState)
    {
        this.InstancePath = InstancePath;
        this.Operation = Operation;
        this.ExpectedFact = ExpectedFact;
        this.ObservedResult = ObservedResult;
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
