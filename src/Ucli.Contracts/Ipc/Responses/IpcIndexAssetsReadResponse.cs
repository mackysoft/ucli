using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one <c>index.assets.read</c> IPC response payload. </summary>
/// <param name="GeneratedAtUtc"> The server-side snapshot generation timestamp. </param>
/// <param name="AssetSearchEntries"> The asset-search snapshot entries. </param>
/// <param name="GuidPathEntries"> The GUID-path snapshot entries. </param>
public sealed record IpcIndexAssetsReadResponse (
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<IndexAssetSearchEntryJsonContract>? AssetSearchEntries,
    IReadOnlyList<IndexGuidPathEntryJsonContract>? GuidPathEntries);