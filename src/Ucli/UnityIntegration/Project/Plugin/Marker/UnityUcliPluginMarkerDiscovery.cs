using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Marker;

/// <summary> Enumerates candidate uCLI Unity plugin marker files from supported Unity project locations. </summary>
internal sealed class UnityUcliPluginMarkerDiscovery
{
    private static readonly string[] StandardMarkerRelativePaths =
    [
        Path.Combine("Assets", "MackySoft", "MackySoft.Ucli.Unity", UnityUcliPluginLocator.MarkerFileName),
        Path.Combine("Packages", UnityUcliPluginLocator.ExpectedPluginId, UnityUcliPluginLocator.MarkerFileName),
        Path.Combine("Assets", "Packages", UnityUcliPluginLocator.ExpectedPluginId, UnityUcliPluginLocator.MarkerFileName),
        Path.Combine("Assets", "Packages", "MackySoft.Ucli.Unity", UnityUcliPluginLocator.MarkerFileName),
    ];

    private static readonly string[] StandardNuGetPackageDirectoryPrefixes =
    [
        $"{UnityUcliPluginLocator.ExpectedPluginId}.",
        "MackySoft.Ucli.Unity.",
    ];

    private static readonly string[] SearchRootDirectoryNames =
    [
        "Assets",
        "Packages",
    ];

    /// <summary> Enumerates candidate marker paths from the supported Unity project roots. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <returns> The enumeration result. </returns>
    public UnityUcliPluginMarkerDiscoveryResult TryEnumerateMarkerPaths (string unityProjectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);

        try
        {
            var pathComparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var markerPaths = new SortedSet<string>(pathComparer);
            CollectStandardMarkerPaths(unityProjectRoot, markerPaths);

            foreach (var searchRootDirectoryName in SearchRootDirectoryNames)
            {
                var searchRootPath = Path.Combine(unityProjectRoot, searchRootDirectoryName);
                if (!Directory.Exists(searchRootPath))
                {
                    continue;
                }

                foreach (var markerPath in Directory.EnumerateFiles(
                             searchRootPath,
                             UnityUcliPluginLocator.MarkerFileName,
                             SearchOption.AllDirectories))
                {
                    markerPaths.Add(Path.GetFullPath(markerPath));
                }
            }

            return UnityUcliPluginMarkerDiscoveryResult.Success(markerPaths.ToArray());
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityUcliPluginMarkerDiscoveryResult.Failure(
                unityProjectRoot,
                ExecutionError.InvalidArgument(
                    $"uCLI Unity plugin marker is invalid. Path='{unityProjectRoot}'. Reason=Marker search path is invalid. {exception.Message}"));
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
        string unityProjectRoot,
        SortedSet<string> markerPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentNullException.ThrowIfNull(markerPaths);

        foreach (var relativeMarkerPath in StandardMarkerRelativePaths)
        {
            var absoluteMarkerPath = Path.Combine(unityProjectRoot, relativeMarkerPath);
            if (!File.Exists(absoluteMarkerPath))
            {
                continue;
            }

            markerPaths.Add(Path.GetFullPath(absoluteMarkerPath));
        }

        CollectVersionedNuGetMarkerPaths(
            Path.Combine(unityProjectRoot, "Assets", "Packages"),
            markerPaths);
    }

    private static void CollectVersionedNuGetMarkerPaths (
        string assetsPackagesRoot,
        SortedSet<string> markerPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetsPackagesRoot);
        ArgumentNullException.ThrowIfNull(markerPaths);

        if (!Directory.Exists(assetsPackagesRoot))
        {
            return;
        }

        foreach (var packageDirectoryPath in Directory.EnumerateDirectories(assetsPackagesRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var packageDirectoryName = Path.GetFileName(packageDirectoryPath);
            if (!IsStandardNuGetPackageDirectoryName(packageDirectoryName))
            {
                continue;
            }

            var markerPath = Path.Combine(packageDirectoryPath, UnityUcliPluginLocator.MarkerFileName);
            if (!File.Exists(markerPath))
            {
                continue;
            }

            markerPaths.Add(Path.GetFullPath(markerPath));
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
