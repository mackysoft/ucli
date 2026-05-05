namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Represents read-index safety requirements produced by mutation operation execution. </summary>
/// <param name="Requirements"> The read requirements that later reads must satisfy. </param>
internal sealed record OperationExecutionReadPostcondition (
    IReadOnlyList<OperationExecutionReadPostconditionRequirement> Requirements);
