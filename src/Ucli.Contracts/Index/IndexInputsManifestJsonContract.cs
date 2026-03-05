namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents persisted <c>inputs/manifest.json</c> contract fields for read-index freshness checks. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="GeneratedAtUtc"> The generated-at timestamp. </param>
/// <param name="ScriptAssembliesHash"> The script-assemblies hash value. </param>
/// <param name="PackagesManifestHash"> The packages-manifest hash value. </param>
/// <param name="PackagesLockHash"> The packages-lock hash value. </param>
/// <param name="AssemblyDefinitionHash"> The asmdef/asmref hash value. </param>
/// <param name="CombinedHash"> The combined input hash value. </param>
internal sealed record IndexInputsManifestJsonContract (
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string? ScriptAssembliesHash,
    string? PackagesManifestHash,
    string? PackagesLockHash,
    string? AssemblyDefinitionHash,
    string? CombinedHash);