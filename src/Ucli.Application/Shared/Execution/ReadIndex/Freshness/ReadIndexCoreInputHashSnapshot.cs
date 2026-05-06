namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one core read-index input fingerprint snapshot that excludes asset lookup hashes. </summary>
internal sealed record ReadIndexCoreInputHashSnapshot (
    string ScriptAssembliesHash,
    string PackagesManifestHash,
    string? PackagesLockHash,
    string AssemblyDefinitionHash,
    string CombinedHash);
