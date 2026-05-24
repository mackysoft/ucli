namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Holds the individual file hashes that form a core input fingerprint. </summary>
internal sealed record IndexCoreInputFileHashes (
    string ScriptAssembliesHash,
    string PackagesManifestHash,
    string PackagesLockHash,
    string AssemblyDefinitionHash);
