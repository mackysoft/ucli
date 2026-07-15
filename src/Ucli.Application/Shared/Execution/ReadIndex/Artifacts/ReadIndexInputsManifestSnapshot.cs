using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one validated persisted read-index input manifest. </summary>
internal sealed record ReadIndexInputsManifestSnapshot
{
    private const int SupportedSchemaVersion = 1;

    private ReadIndexInputsManifestSnapshot (
        DateTimeOffset generatedAtUtc,
        ReadIndexInputHashSnapshot hashes)
    {
        GeneratedAtUtc = generatedAtUtc;
        Hashes = hashes;
    }

    /// <summary> Gets the UTC time at which the manifest was generated. </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary> Gets the validated input hashes recorded by the manifest. </summary>
    public ReadIndexInputHashSnapshot Hashes { get; }

    /// <summary> Projects a persisted input-manifest contract when every field is canonical. </summary>
    public static bool TryCreate (
        IndexInputsManifestJsonContract? contract,
        [NotNullWhen(true)] out ReadIndexInputsManifestSnapshot? snapshot)
    {
        snapshot = null;
        if (contract == null
            || contract.SchemaVersion != SupportedSchemaVersion
            || contract.GeneratedAtUtc == default
            || contract.GeneratedAtUtc.Offset != TimeSpan.Zero
            || !Sha256Digest.TryParse(contract.ScriptAssembliesHash, out var scriptAssembliesHash)
            || !Sha256Digest.TryParse(contract.PackagesManifestHash, out var packagesManifestHash)
            || !Sha256Digest.TryParse(contract.PackagesLockHash, out var packagesLockHash)
            || !Sha256Digest.TryParse(contract.AssemblyDefinitionHash, out var assemblyDefinitionHash)
            || !Sha256Digest.TryParse(contract.AssetsContentHash, out var assetsContentHash)
            || !Sha256Digest.TryParse(contract.AssetSearchHash, out var assetSearchHash)
            || !Sha256Digest.TryParse(contract.GuidPathHash, out var guidPathHash)
            || !Sha256Digest.TryParse(contract.CombinedHash, out var combinedHash))
        {
            return false;
        }

        snapshot = new ReadIndexInputsManifestSnapshot(
            contract.GeneratedAtUtc,
            new ReadIndexInputHashSnapshot(
                scriptAssembliesHash,
                packagesManifestHash,
                packagesLockHash,
                assemblyDefinitionHash,
                assetsContentHash,
                assetSearchHash,
                guidPathHash,
                combinedHash));
        return true;
    }
}
