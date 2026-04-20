using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

namespace MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents normalized output for <c>ops list</c>. </summary>
/// <param name="Operations"> The available operations. </param>
/// <param name="ReadIndex"> The read-index metadata. </param>
internal sealed record OpsListExecutionOutput (
    IReadOnlyList<OpsOperationListItem> Operations,
    ReadIndexInfo ReadIndex);