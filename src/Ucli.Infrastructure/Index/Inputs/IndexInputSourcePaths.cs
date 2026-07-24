using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Holds filesystem source paths used to compute read-index input fingerprints. </summary>
internal sealed record IndexInputSourcePaths (
    AbsolutePath ScriptAssembliesPath,
    AbsolutePath PackagesManifestPath,
    AbsolutePath PackagesLockPath,
    AbsolutePath AssetsPath,
    AbsolutePath PackagesPath)
{
    /// <summary> Creates guarded source paths from one Unity project root. </summary>
    public static IndexInputSourcePaths FromProjectRoot (AbsolutePath projectRoot)
    {
        return new IndexInputSourcePaths(
            ScriptAssembliesPath: Resolve(projectRoot, "Library/ScriptAssemblies"),
            PackagesManifestPath: Resolve(projectRoot, "Packages/manifest.json"),
            PackagesLockPath: Resolve(projectRoot, "Packages/packages-lock.json"),
            AssetsPath: Resolve(projectRoot, "Assets"),
            PackagesPath: Resolve(projectRoot, "Packages"));
    }

    private static AbsolutePath Resolve (
        AbsolutePath projectRoot,
        string relativePath)
    {
        return ContainedPath.Create(projectRoot, RootRelativePath.Parse(relativePath)).Target;
    }
}
