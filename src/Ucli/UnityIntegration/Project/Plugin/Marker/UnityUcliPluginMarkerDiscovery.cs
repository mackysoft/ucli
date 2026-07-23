using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Marker;

/// <summary> Enumerates candidate uCLI Unity plugin marker files from supported Unity project locations. </summary>
internal sealed class UnityUcliPluginMarkerDiscovery
{
    private static readonly RootRelativePath[] StandardMarkerRelativePaths =
    [
        RootRelativePath.Parse($"Assets/MackySoft/MackySoft.Ucli.Unity/{UnityUcliPluginMarkerContract.MarkerFileName}"),
        RootRelativePath.Parse($"Packages/{UnityUcliPluginMarkerContract.ExpectedPluginId}/{UnityUcliPluginMarkerContract.MarkerFileName}"),
        RootRelativePath.Parse($"Assets/Packages/{UnityUcliPluginMarkerContract.ExpectedPluginId}/{UnityUcliPluginMarkerContract.MarkerFileName}"),
        RootRelativePath.Parse($"Assets/Packages/MackySoft.Ucli.Unity/{UnityUcliPluginMarkerContract.MarkerFileName}"),
    ];

    private static readonly string[] StandardNuGetPackageDirectoryPrefixes =
    [
        $"{UnityUcliPluginMarkerContract.ExpectedPluginId}.",
        "MackySoft.Ucli.Unity.",
    ];

    private static readonly RootRelativePath[] SearchRootDirectoryPaths =
    [
        RootRelativePath.Parse("Assets"),
        RootRelativePath.Parse("Packages"),
    ];

    /// <summary> Enumerates candidate marker paths from the supported Unity project roots. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <returns> The enumeration result. </returns>
    public UnityUcliPluginMarkerDiscoveryResult TryEnumerateMarkerPaths (AbsolutePath unityProjectRoot)
    {
        try
        {
            var markerPaths = new List<AbsolutePath>();
            var markerPathSet = new HashSet<AbsolutePath>();
            CollectStandardMarkerPaths(unityProjectRoot, markerPaths);
            foreach (var markerPath in markerPaths)
            {
                markerPathSet.Add(markerPath);
            }

            foreach (var searchRootDirectoryPath in SearchRootDirectoryPaths)
            {
                var searchRootPath = ContainedPath.Create(
                    unityProjectRoot,
                    searchRootDirectoryPath).Target;
                if (!Directory.Exists(searchRootPath.Value))
                {
                    continue;
                }

                foreach (var markerPath in Directory.EnumerateFiles(
                             searchRootPath.Value,
                             UnityUcliPluginMarkerContract.MarkerFileName,
                             SearchOption.AllDirectories)
                         .OrderBy(static path => path, StringComparer.Ordinal))
                {
                    var absoluteMarkerPath = AbsolutePath.Parse(markerPath);
                    if (markerPathSet.Add(absoluteMarkerPath))
                    {
                        markerPaths.Add(absoluteMarkerPath);
                    }
                }
            }

            return UnityUcliPluginMarkerDiscoveryResult.Success(markerPaths);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return UnityUcliPluginMarkerDiscoveryResult.Failure(
                unityProjectRoot,
                ExecutionError.InvalidArgument(
                    $"uCLI Unity plugin marker is invalid. Path='{unityProjectRoot}'. Reason=Marker search failed. {exception.Message}"));
        }
    }

    private static void CollectStandardMarkerPaths (
        AbsolutePath unityProjectRoot,
        ICollection<AbsolutePath> markerPaths)
    {
        ArgumentNullException.ThrowIfNull(markerPaths);

        foreach (var relativeMarkerPath in StandardMarkerRelativePaths)
        {
            var absoluteMarkerPath = ContainedPath.Create(unityProjectRoot, relativeMarkerPath).Target;
            if (!File.Exists(absoluteMarkerPath.Value))
            {
                continue;
            }

            markerPaths.Add(absoluteMarkerPath);
        }

        CollectVersionedNuGetMarkerPaths(
            ContainedPath.Create(
                unityProjectRoot,
                RootRelativePath.Parse("Assets/Packages")).Target,
            markerPaths);
    }

    private static void CollectVersionedNuGetMarkerPaths (
        AbsolutePath assetsPackagesRoot,
        ICollection<AbsolutePath> markerPaths)
    {
        ArgumentNullException.ThrowIfNull(markerPaths);

        if (!Directory.Exists(assetsPackagesRoot.Value))
        {
            return;
        }

        foreach (var packageDirectoryValue in Directory.EnumerateDirectories(
                     assetsPackagesRoot.Value,
                     "*",
                     SearchOption.TopDirectoryOnly)
                 .OrderBy(static path => path, StringComparer.Ordinal))
        {
            var packageDirectoryPath = AbsolutePath.Parse(packageDirectoryValue);
            var packageDirectoryName = Path.GetFileName(packageDirectoryPath.Value);
            if (!IsStandardNuGetPackageDirectoryName(packageDirectoryName))
            {
                continue;
            }

            var markerPath = ContainedPath.Create(
                packageDirectoryPath,
                RootRelativePath.Parse(UnityUcliPluginMarkerContract.MarkerFileName)).Target;
            if (!File.Exists(markerPath.Value))
            {
                continue;
            }

            markerPaths.Add(markerPath);
        }
    }

    private static bool IsStandardNuGetPackageDirectoryName (string? packageDirectoryName)
    {
        if (string.IsNullOrWhiteSpace(packageDirectoryName))
        {
            return false;
        }

        foreach (var standardPrefix in StandardNuGetPackageDirectoryPrefixes)
        {
            if (packageDirectoryName.StartsWith(standardPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
