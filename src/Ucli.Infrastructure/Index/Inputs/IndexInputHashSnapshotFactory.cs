using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Creates read-index input snapshots from validated hash components. </summary>
internal static class IndexInputHashSnapshotFactory
{
    /// <summary> Creates one core input snapshot from core file hashes. </summary>
    public static IndexCoreInputHashSnapshot CreateCore (IndexCoreInputFileHashes hashes)
    {
        if (hashes == null)
        {
            throw new ArgumentNullException(nameof(hashes));
        }
        var combinedHash = IndexInputFileHasher.ComputeUtf8Hash(string.Concat(
            hashes.ScriptAssembliesHash,
            "\n",
            hashes.PackagesManifestHash,
            "\n",
            hashes.PackagesLockHash,
            "\n",
            hashes.AssemblyDefinitionHash));
        return new IndexCoreInputHashSnapshot(
            hashes.ScriptAssembliesHash,
            hashes.PackagesManifestHash,
            hashes.PackagesLockHash,
            hashes.AssemblyDefinitionHash,
            combinedHash);
    }

    /// <summary> Creates one full input snapshot from core and asset-content hashes. </summary>
    public static IndexInputHashSnapshot Create (
        IndexCoreInputHashSnapshot coreSnapshot,
        Sha256Digest assetsContentHash)
    {
        if (coreSnapshot == null)
        {
            throw new ArgumentNullException(nameof(coreSnapshot));
        }

        if (assetsContentHash == null)
        {
            throw new ArgumentNullException(nameof(assetsContentHash));
        }
        var assetSearchHash = IndexInputFileHasher.ComputeUtf8Hash(string.Concat(
            coreSnapshot.CombinedHash,
            "\n",
            assetsContentHash));
        var guidPathHash = IndexInputFileHasher.ComputeUtf8Hash(assetsContentHash.ToString());
        return new IndexInputHashSnapshot(
            coreSnapshot.ScriptAssembliesHash,
            coreSnapshot.PackagesManifestHash,
            coreSnapshot.PackagesLockHash,
            coreSnapshot.AssemblyDefinitionHash,
            assetsContentHash,
            assetSearchHash,
            guidPathHash,
            coreSnapshot.CombinedHash);
    }
}
