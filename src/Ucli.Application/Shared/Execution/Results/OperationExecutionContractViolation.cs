using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.Results;

/// <summary> Represents one operation-result violation against published assurance facts. </summary>
/// <param name="InstancePath"> The RFC 6901 path of the operation result associated with the violation. </param>
/// <param name="Operation"> The operation name whose runtime result violated its contract. </param>
/// <param name="ExpectedFact"> The assurance fact expected by the operation metadata. </param>
/// <param name="ObservedResult"> The observed result fact that contradicted the expected fact. </param>
/// <param name="ApplicationState"> The application state used to decide retry safety. </param>
internal sealed record OperationExecutionContractViolation (
    string InstancePath,
    string Operation,
    string ExpectedFact,
    string ObservedResult,
    IpcApplicationState ApplicationState);
