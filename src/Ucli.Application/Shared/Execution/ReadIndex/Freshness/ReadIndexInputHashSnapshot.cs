namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one read-index input fingerprint snapshot. </summary>
internal sealed record ReadIndexInputHashSnapshot (
    string ScriptAssembliesHash,
    string PackagesManifestHash,
    string? PackagesLockHash,
    string AssemblyDefinitionHash,
    string AssetsContentHash,
    string AssetSearchHash,
    string GuidPathHash,
    string CombinedHash);
