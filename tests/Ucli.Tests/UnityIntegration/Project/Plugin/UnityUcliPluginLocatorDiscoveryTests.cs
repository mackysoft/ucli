using MackySoft.Tests;
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
        await WriteMarkerAsync(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.NotNull(result.MarkerPath);
        Assert.EndsWith(
            Path.Combine("Assets", "MackySoft", "MackySoft.Ucli.Unity", UnityUcliPluginMarkerContract.MarkerFileName),
            result.MarkerPath,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMarkerExistsUnderPackages_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "packages-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarkerAsync(scope, Path.Combine("UnityProject", "Packages", "com.mackysoft.ucli.unity"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.NotNull(result.MarkerPath);
        Assert.EndsWith(
            Path.Combine("Packages", "com.mackysoft.ucli.unity", UnityUcliPluginMarkerContract.MarkerFileName),
            result.MarkerPath,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMarkerExistsUnderAssetsPackages_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "assets-packages-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarkerAsync(scope, Path.Combine("UnityProject", "Assets", "Packages", "com.mackysoft.ucli.unity.1.0.0"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.NotNull(result.MarkerPath);
        Assert.EndsWith(
            Path.Combine("Assets", "Packages", "com.mackysoft.ucli.unity.1.0.0", UnityUcliPluginMarkerContract.MarkerFileName),
            result.MarkerPath,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMarkerExistsUnderAssetsPackagesWithPascalPackageId_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "assets-packages-pascal-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarkerAsync(scope, Path.Combine("UnityProject", "Assets", "Packages", "MackySoft.Ucli.Unity.1.0.0"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var result = await locator.LocateAsync(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.NotNull(result.MarkerPath);
        Assert.EndsWith(
            Path.Combine("Assets", "Packages", "MackySoft.Ucli.Unity.1.0.0", UnityUcliPluginMarkerContract.MarkerFileName),
            result.MarkerPath,
            StringComparison.Ordinal);
    }
}
