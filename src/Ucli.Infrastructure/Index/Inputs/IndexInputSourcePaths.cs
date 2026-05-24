namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Holds filesystem source paths used to compute read-index input fingerprints. </summary>
internal sealed record IndexInputSourcePaths (
    string ScriptAssembliesPath,
    string PackagesManifestPath,
    string PackagesLockPath,
    string AssetsPath,
    string PackagesPath)
{
    /// <summary> Creates source paths from one normalized Unity project root path. </summary>
    public static IndexInputSourcePaths FromNormalizedProjectRoot (string normalizedProjectRoot)
    {
        return new IndexInputSourcePaths(
            ScriptAssembliesPath: Path.Combine(normalizedProjectRoot, "Library", "ScriptAssemblies"),
            PackagesManifestPath: Path.Combine(normalizedProjectRoot, "Packages", "manifest.json"),
            PackagesLockPath: Path.Combine(normalizedProjectRoot, "Packages", "packages-lock.json"),
            AssetsPath: Path.Combine(normalizedProjectRoot, "Assets"),
            PackagesPath: Path.Combine(normalizedProjectRoot, "Packages"));
    }
}
