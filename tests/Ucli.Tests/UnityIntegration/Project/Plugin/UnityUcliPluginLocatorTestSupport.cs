using System.Text.Json;
using MackySoft.Tests;
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
        string markerDirectoryRelativePath,
        string? contents = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerDirectoryRelativePath);

        return scope.WriteFileAsync(
            Path.Combine(markerDirectoryRelativePath, UnityUcliPluginMarkerContract.MarkerFileName),
            contents
            ?? """
               {
                 "pluginId": "com.mackysoft.ucli.unity",
                 "protocolVersion": 1
               }
               """);
    }

    public static async Task<UnityUcliPluginMarkerCache> ReadCacheAsync (
        TestDirectoryScope scope,
        string unityProjectPath)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectPath);

        var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(unityProjectPath);
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, unityProjectPath);
        var cachePath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(storageRoot, projectFingerprint);
        var json = await File.ReadAllTextAsync(cachePath);
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
