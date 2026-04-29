namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one core input-fingerprint snapshot that excludes asset lookup hashes. </summary>
/// <param name="ScriptAssembliesHash"> The script-assemblies hash value. </param>
/// <param name="PackagesManifestHash"> The packages-manifest hash value. </param>
/// <param name="PackagesLockHash"> The packages-lock hash value. </param>
/// <param name="AssemblyDefinitionHash"> The asmdef/asmref hash value. </param>
/// <param name="CombinedHash"> The combined hash value. </param>
internal sealed record IndexCoreInputHashSnapshot (
    string ScriptAssembliesHash,
    string PackagesManifestHash,
    string PackagesLockHash,
    string AssemblyDefinitionHash,
    string CombinedHash);
