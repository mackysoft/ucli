using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one read-index input fingerprint snapshot. </summary>
internal sealed record ReadIndexInputHashSnapshot
{
    public ReadIndexInputHashSnapshot (
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

    public ReadIndexInputHashSnapshot WithAssetHashes (
        Sha256Digest assetsContentHash,
        Sha256Digest assetSearchHash,
        Sha256Digest guidPathHash)
    {
        return new ReadIndexInputHashSnapshot(
            ScriptAssembliesHash,
            PackagesManifestHash,
            PackagesLockHash,
            AssemblyDefinitionHash,
            assetsContentHash,
            assetSearchHash,
            guidPathHash,
            CombinedHash);
    }

    public ReadIndexInputHashSnapshot WithCoreHashes (ReadIndexInputHashSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ReadIndexInputHashSnapshot(
            source.ScriptAssembliesHash,
            source.PackagesManifestHash,
            source.PackagesLockHash,
            source.AssemblyDefinitionHash,
            AssetsContentHash,
            AssetSearchHash,
            GuidPathHash,
            source.CombinedHash);
    }
}
