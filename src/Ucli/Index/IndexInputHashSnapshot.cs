namespace MackySoft.Ucli.Index;

/// <summary> Represents one current input-hash snapshot used for index freshness checks. </summary>
/// <param name="ScriptAssembliesHash"> The script-assemblies hash value. </param>
/// <param name="PackagesManifestHash"> The packages-manifest hash value. </param>
/// <param name="PackagesLockHash"> The packages-lock hash value. </param>
/// <param name="AssemblyDefinitionHash"> The asmdef/asmref hash value. </param>
/// <param name="CombinedHash"> The combined hash value. </param>
internal sealed record IndexInputHashSnapshot (
    string ScriptAssembliesHash,
    string PackagesManifestHash,
    string PackagesLockHash,
    string AssemblyDefinitionHash,
    string CombinedHash);