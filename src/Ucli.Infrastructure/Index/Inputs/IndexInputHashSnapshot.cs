using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Represents one input-fingerprint snapshot used for read-index freshness checks. </summary>
/// <param name="ScriptAssembliesHash"> The script-assemblies hash value. </param>
/// <param name="PackagesManifestHash"> The packages-manifest hash value. </param>
/// <param name="PackagesLockHash"> The packages-lock hash value. </param>
/// <param name="AssemblyDefinitionHash"> The asmdef/asmref hash value. </param>
/// <param name="AssetsContentHash"> The asset-content hash value for files under <c>Assets/</c>. </param>
/// <param name="AssetSearchHash"> The combined hash value used by <c>asset-search.lookup.json</c>. </param>
/// <param name="GuidPathHash"> The combined hash value used by <c>guid-path.lookup.json</c>. </param>
/// <param name="CombinedHash"> The combined hash value. </param>
internal sealed record IndexInputHashSnapshot
{
    public IndexInputHashSnapshot (
        Sha256Digest scriptAssembliesHash,
        Sha256Digest packagesManifestHash,
        Sha256Digest packagesLockHash,
        Sha256Digest assemblyDefinitionHash,
        Sha256Digest assetsContentHash,
        Sha256Digest assetSearchHash,
        Sha256Digest guidPathHash,
        Sha256Digest combinedHash)
    {
        ScriptAssembliesHash = scriptAssembliesHash ?? throw new ArgumentNullException(nameof(scriptAssembliesHash));
        PackagesManifestHash = packagesManifestHash ?? throw new ArgumentNullException(nameof(packagesManifestHash));
        PackagesLockHash = packagesLockHash ?? throw new ArgumentNullException(nameof(packagesLockHash));
        AssemblyDefinitionHash = assemblyDefinitionHash ?? throw new ArgumentNullException(nameof(assemblyDefinitionHash));
        AssetsContentHash = assetsContentHash ?? throw new ArgumentNullException(nameof(assetsContentHash));
        AssetSearchHash = assetSearchHash ?? throw new ArgumentNullException(nameof(assetSearchHash));
        GuidPathHash = guidPathHash ?? throw new ArgumentNullException(nameof(guidPathHash));
        CombinedHash = combinedHash ?? throw new ArgumentNullException(nameof(combinedHash));
    }

    public Sha256Digest ScriptAssembliesHash { get; }

    public Sha256Digest PackagesManifestHash { get; }

    public Sha256Digest PackagesLockHash { get; }

    public Sha256Digest AssemblyDefinitionHash { get; }

    public Sha256Digest AssetsContentHash { get; }

    public Sha256Digest AssetSearchHash { get; }

    public Sha256Digest GuidPathHash { get; }

    public Sha256Digest CombinedHash { get; }
}
