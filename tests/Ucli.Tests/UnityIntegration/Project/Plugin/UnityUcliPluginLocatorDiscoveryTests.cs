using MackySoft.FileSystem;
using MackySoft.Ucli.Tests.Helpers.Unity;
using static MackySoft.Ucli.Tests.UnityUcliPluginLocatorTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class UnityUcliPluginLocatorDiscoveryTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMarkerExistsUnderAssets_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "assets-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var unityProjectRoot = AbsolutePath.Parse(unityProjectPath);
        var markerDirectoryRelativePath = RootRelativePath.Parse(
            Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        await WriteMarkerAsync(scope, markerDirectoryRelativePath);
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var result = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.Equal(
            ResolveMarkerPath(
                unityProjectRoot,
                RootRelativePath.Parse(Path.Combine("Assets", "MackySoft", "MackySoft.Ucli.Unity"))),
            result.MarkerPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMarkerExistsUnderPackages_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "packages-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var unityProjectRoot = AbsolutePath.Parse(unityProjectPath);
        var markerDirectoryRelativePath = RootRelativePath.Parse(
            Path.Combine("UnityProject", "Packages", "com.mackysoft.ucli.unity"));
        await WriteMarkerAsync(scope, markerDirectoryRelativePath);
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var result = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.Equal(
            ResolveMarkerPath(
                unityProjectRoot,
                RootRelativePath.Parse(Path.Combine("Packages", "com.mackysoft.ucli.unity"))),
            result.MarkerPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMarkerExistsUnderAssetsPackages_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "assets-packages-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var unityProjectRoot = AbsolutePath.Parse(unityProjectPath);
        var markerDirectoryRelativePath = RootRelativePath.Parse(
            Path.Combine("UnityProject", "Assets", "Packages", "com.mackysoft.ucli.unity.1.0.0"));
        await WriteMarkerAsync(scope, markerDirectoryRelativePath);
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var result = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.Equal(
            ResolveMarkerPath(
                unityProjectRoot,
                RootRelativePath.Parse(Path.Combine("Assets", "Packages", "com.mackysoft.ucli.unity.1.0.0"))),
            result.MarkerPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMarkerExistsUnderAssetsPackagesWithPascalPackageId_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "assets-packages-pascal-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var unityProjectRoot = AbsolutePath.Parse(unityProjectPath);
        var markerDirectoryRelativePath = RootRelativePath.Parse(
            Path.Combine("UnityProject", "Assets", "Packages", "MackySoft.Ucli.Unity.1.0.0"));
        await WriteMarkerAsync(scope, markerDirectoryRelativePath);
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var result = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.Equal(
            ResolveMarkerPath(
                unityProjectRoot,
                RootRelativePath.Parse(Path.Combine("Assets", "Packages", "MackySoft.Ucli.Unity.1.0.0"))),
            result.MarkerPath);
    }
}
