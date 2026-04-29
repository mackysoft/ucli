namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one input-fingerprint snapshot used for read-index freshness checks. </summary>
/// <param name="ScriptAssembliesHash"> The script-assemblies hash value. </param>
/// <param name="PackagesManifestHash"> The packages-manifest hash value. </param>
/// <param name="PackagesLockHash"> The packages-lock hash value. </param>
/// <param name="AssemblyDefinitionHash"> The asmdef/asmref hash value. </param>
/// <param name="AssetsContentHash"> The asset-content hash value for files under <c>Assets/</c>. </param>
/// <param name="AssetSearchHash"> The combined hash value used by <c>asset-search.lookup.json</c>. </param>
/// <param name="GuidPathHash"> The combined hash value used by <c>guid-path.lookup.json</c>. </param>
/// <param name="CombinedHash"> The combined hash value. </param>
internal sealed record IndexInputHashSnapshot (
    string ScriptAssembliesHash,
    string PackagesManifestHash,
    string PackagesLockHash,
    string AssemblyDefinitionHash,
    string AssetsContentHash,
    string AssetSearchHash,
    string GuidPathHash,
    string CombinedHash);
