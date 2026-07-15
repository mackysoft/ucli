using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Holds the individual file hashes that form a core input fingerprint. </summary>
internal sealed record IndexCoreInputFileHashes
{
    public IndexCoreInputFileHashes (
        Sha256Digest scriptAssembliesHash,
        Sha256Digest packagesManifestHash,
        Sha256Digest packagesLockHash,
        Sha256Digest assemblyDefinitionHash)
    {
        ScriptAssembliesHash = scriptAssembliesHash ?? throw new ArgumentNullException(nameof(scriptAssembliesHash));
        PackagesManifestHash = packagesManifestHash ?? throw new ArgumentNullException(nameof(packagesManifestHash));
        PackagesLockHash = packagesLockHash ?? throw new ArgumentNullException(nameof(packagesLockHash));
        AssemblyDefinitionHash = assemblyDefinitionHash ?? throw new ArgumentNullException(nameof(assemblyDefinitionHash));
    }

    public Sha256Digest ScriptAssembliesHash { get; }

    public Sha256Digest PackagesManifestHash { get; }

    public Sha256Digest PackagesLockHash { get; }

    public Sha256Digest AssemblyDefinitionHash { get; }
}
