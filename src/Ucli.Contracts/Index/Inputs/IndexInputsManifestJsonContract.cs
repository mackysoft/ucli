namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents persisted <c>inputs/manifest.json</c> contract fields for input-fingerprint bookkeeping. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="GeneratedAtUtc"> The generated-at timestamp. </param>
/// <param name="ScriptAssembliesHash"> The script-assemblies hash value. </param>
/// <param name="PackagesManifestHash"> The packages-manifest hash value. </param>
/// <param name="PackagesLockHash"> The packages-lock hash value. </param>
/// <param name="AssemblyDefinitionHash"> The asmdef/asmref hash value. </param>
/// <param name="AssetsContentHash"> The asset-content hash value for files under <c>Assets/</c>. </param>
/// <param name="AssetSearchHash"> The combined hash value used by <c>asset-search.lookup.json</c>. </param>
/// <param name="GuidPathHash"> The combined hash value used by <c>guid-path.lookup.json</c>. </param>
/// <param name="CombinedHash"> The combined input hash value. </param>
internal sealed record IndexInputsManifestJsonContract (
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string? ScriptAssembliesHash,
    string? PackagesManifestHash,
    string? PackagesLockHash,
    string? AssemblyDefinitionHash,
    string? AssetsContentHash,
    string? AssetSearchHash,
    string? GuidPathHash,
    string? CombinedHash);
