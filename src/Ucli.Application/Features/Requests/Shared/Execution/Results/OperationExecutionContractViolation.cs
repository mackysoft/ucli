namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Represents one operation-result violation against published assurance facts. </summary>
/// <param name="OpId"> The public operation identifier associated with the violation. </param>
/// <param name="Operation"> The operation name whose runtime result violated its contract. </param>
/// <param name="ExpectedFact"> The assurance fact expected by the operation metadata. </param>
/// <param name="ObservedResult"> The observed result fact that contradicted the expected fact. </param>
/// <param name="ApplicationState"> The application-state literal used to decide retry safety. </param>
internal sealed record OperationExecutionContractViolation (
    string OpId,
    string Operation,
    string ExpectedFact,
    string ObservedResult,
    string ApplicationState);
