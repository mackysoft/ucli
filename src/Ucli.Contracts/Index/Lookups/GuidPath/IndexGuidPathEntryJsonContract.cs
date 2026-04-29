namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one GUID-path lookup entry. </summary>
/// <param name="AssetGuid"> The asset GUID. </param>
/// <param name="AssetPath"> The persistent main asset path under <c>Assets/</c>. </param>
public sealed record IndexGuidPathEntryJsonContract (
    string? AssetGuid,
    string? AssetPath);
