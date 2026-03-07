using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Ops;

/// <summary> Represents the normalized catalog snapshot used by <c>ops</c> subcommands. </summary>
/// <param name="Operations"> The available operation entries. </param>
/// <param name="ReadIndex"> The read-index metadata emitted to command payloads. </param>
internal sealed record OpsCatalogReadOutput (
    IReadOnlyList<IndexOpEntryJsonContract> Operations,
    OpsReadIndexInfo ReadIndex);