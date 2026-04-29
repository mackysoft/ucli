namespace MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents normalized output for <c>ops describe</c>. </summary>
/// <param name="Operation"> The described operation. </param>
/// <param name="ReadIndex"> The read-index metadata. </param>
internal sealed record OpsDescribeExecutionOutput (
    OpsOperationDetail Operation,
    ReadIndexInfo ReadIndex);
