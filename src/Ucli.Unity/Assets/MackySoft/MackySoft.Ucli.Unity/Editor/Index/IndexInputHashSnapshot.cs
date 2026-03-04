#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Represents one hash snapshot used to stamp index catalog freshness inputs. </summary>
    /// <param name="ScriptAssembliesHash"> The hash of files under <c>Library/ScriptAssemblies</c>. </param>
    /// <param name="PackagesManifestHash"> The hash of <c>Packages/manifest.json</c>. </param>
    /// <param name="PackagesLockHash"> The hash of <c>Packages/packages-lock.json</c>. </param>
    /// <param name="AssemblyDefinitionHash"> The hash of all <c>.asmdef</c>/<c>.asmref</c> files. </param>
    /// <param name="CombinedHash"> The combined hash value. </param>
    internal sealed record IndexInputHashSnapshot (
        string ScriptAssembliesHash,
        string PackagesManifestHash,
        string PackagesLockHash,
        string AssemblyDefinitionHash,
        string CombinedHash);
}
