namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one asset-search lookup entry. </summary>
/// <param name="AssetPath"> The persistent main asset path under <c>Assets/</c>. </param>
/// <param name="AssetGuid"> The asset GUID resolved from <paramref name="AssetPath" />. </param>
/// <param name="Name"> The main asset name. </param>
/// <param name="TypeId"> The runtime type identifier. </param>
/// <param name="SearchTypeIds"> The assignable runtime/base type identifiers used for offline search. </param>
public sealed record IndexAssetSearchEntryJsonContract (
    string? AssetPath,
    string? AssetGuid,
    string? Name,
    string? TypeId,
    IReadOnlyList<string>? SearchTypeIds);