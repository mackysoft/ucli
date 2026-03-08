using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>ops.read</c> IPC response payload. </summary>
/// <param name="GeneratedAtUtc"> The server-side snapshot generation timestamp. </param>
/// <param name="Operations"> The discovered operations snapshot. </param>
public sealed record IpcOpsReadResponse (
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<IndexOpEntryJsonContract>? Operations);
