using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;

namespace MackySoft.Ucli.Tests;

internal static class UnityUcliPluginLocatorTestSupport
{
    public static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    public static Task WriteMarkerAsync (
        TestDirectoryScope scope,
        RootRelativePath markerDirectoryRelativePath,
        string? contents = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(markerDirectoryRelativePath);

        return scope.WriteFileAsync(
            Path.Combine(markerDirectoryRelativePath.Value, UnityUcliPluginMarkerContract.MarkerFileName),
            contents
            ?? """
               {
                 "pluginId": "com.mackysoft.ucli.unity",
                 "protocolVersion": 1
               }
               """);
    }

    public static AbsolutePath ResolveMarkerPath (
        AbsolutePath unityProjectRoot,
        RootRelativePath markerDirectoryRelativePath)
    {
        ArgumentNullException.ThrowIfNull(unityProjectRoot);
        ArgumentNullException.ThrowIfNull(markerDirectoryRelativePath);

        var markerRelativePath = RootRelativePath.Parse(
            Path.Combine(
                markerDirectoryRelativePath.Value,
                UnityUcliPluginMarkerContract.MarkerFileName));
        return ContainedPath.Create(unityProjectRoot, markerRelativePath).Target;
    }

    public static async Task<UnityUcliPluginMarkerCache> ReadCacheAsync (AbsolutePath unityProjectRoot)
    {
        ArgumentNullException.ThrowIfNull(unityProjectRoot);

        var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(unityProjectRoot);
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, unityProjectRoot);
        var cachePath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(storageRoot, projectFingerprint);
        var json = await File.ReadAllTextAsync(cachePath.Value);
        return JsonSerializer.Deserialize<UnityUcliPluginMarkerCache>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                })
            ?? throw new InvalidOperationException("Plugin marker cache JSON was null.");
    }

    public static UnityUcliPluginLocator CreateLocator ()
    {
        return CreateLocator(new UnityUcliPluginMarkerCacheStore());
    }

    public static UnityUcliPluginLocator CreateLocator (UnityUcliPluginMarkerCacheStore cacheStore)
    {
        var markerValidator = new UnityUcliPluginMarkerValidator();
        return new UnityUcliPluginLocator(
            new UnityUcliPluginMarkerDiscovery(),
            markerValidator,
            new UnityUcliPluginMarkerCacheCoordinator(cacheStore, markerValidator));
    }
}
