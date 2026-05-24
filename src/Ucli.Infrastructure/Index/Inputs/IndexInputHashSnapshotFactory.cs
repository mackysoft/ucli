namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Creates read-index input snapshots from validated hash components. </summary>
internal static class IndexInputHashSnapshotFactory
{
    /// <summary> Creates one core input snapshot from core file hashes. </summary>
    public static IndexCoreInputHashSnapshot CreateCore (IndexCoreInputFileHashes hashes)
    {
        var combinedHash = IndexInputFileHasher.ComputeUtf8Hash(string.Concat(
            hashes.ScriptAssembliesHash,
            "\n",
            hashes.PackagesManifestHash,
            "\n",
            hashes.PackagesLockHash,
            "\n",
            hashes.AssemblyDefinitionHash));
        return new IndexCoreInputHashSnapshot(
            ScriptAssembliesHash: hashes.ScriptAssembliesHash,
            PackagesManifestHash: hashes.PackagesManifestHash,
            PackagesLockHash: hashes.PackagesLockHash,
            AssemblyDefinitionHash: hashes.AssemblyDefinitionHash,
            CombinedHash: combinedHash);
    }

    /// <summary> Creates one full input snapshot from core and asset-content hashes. </summary>
    public static IndexInputHashSnapshot Create (
        IndexCoreInputHashSnapshot coreSnapshot,
        string assetsContentHash)
    {
        var assetSearchHash = IndexInputFileHasher.ComputeUtf8Hash(string.Concat(
            coreSnapshot.CombinedHash,
            "\n",
            assetsContentHash));
        var guidPathHash = IndexInputFileHasher.ComputeUtf8Hash(assetsContentHash);
        return new IndexInputHashSnapshot(
            ScriptAssembliesHash: coreSnapshot.ScriptAssembliesHash,
            PackagesManifestHash: coreSnapshot.PackagesManifestHash,
            PackagesLockHash: coreSnapshot.PackagesLockHash,
            AssemblyDefinitionHash: coreSnapshot.AssemblyDefinitionHash,
            AssetsContentHash: assetsContentHash,
            AssetSearchHash: assetSearchHash,
            GuidPathHash: guidPathHash,
            CombinedHash: coreSnapshot.CombinedHash);
    }
}
