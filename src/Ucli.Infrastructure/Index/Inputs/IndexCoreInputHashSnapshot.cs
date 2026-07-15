using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Represents one core input-fingerprint snapshot that excludes asset lookup hashes. </summary>
/// <param name="ScriptAssembliesHash"> The script-assemblies hash value. </param>
/// <param name="PackagesManifestHash"> The packages-manifest hash value. </param>
/// <param name="PackagesLockHash"> The packages-lock hash value. </param>
/// <param name="AssemblyDefinitionHash"> The asmdef/asmref hash value. </param>
/// <param name="CombinedHash"> The combined hash value. </param>
internal sealed record IndexCoreInputHashSnapshot
{
    public IndexCoreInputHashSnapshot (
        Sha256Digest scriptAssembliesHash,
        Sha256Digest packagesManifestHash,
        Sha256Digest packagesLockHash,
        Sha256Digest assemblyDefinitionHash,
        Sha256Digest combinedHash)
    {
        ScriptAssembliesHash = scriptAssembliesHash ?? throw new ArgumentNullException(nameof(scriptAssembliesHash));
        PackagesManifestHash = packagesManifestHash ?? throw new ArgumentNullException(nameof(packagesManifestHash));
        PackagesLockHash = packagesLockHash ?? throw new ArgumentNullException(nameof(packagesLockHash));
        AssemblyDefinitionHash = assemblyDefinitionHash ?? throw new ArgumentNullException(nameof(assemblyDefinitionHash));
        CombinedHash = combinedHash ?? throw new ArgumentNullException(nameof(combinedHash));
    }

    public Sha256Digest ScriptAssembliesHash { get; }

    public Sha256Digest PackagesManifestHash { get; }

    public Sha256Digest PackagesLockHash { get; }

    public Sha256Digest AssemblyDefinitionHash { get; }

    public Sha256Digest CombinedHash { get; }
}
