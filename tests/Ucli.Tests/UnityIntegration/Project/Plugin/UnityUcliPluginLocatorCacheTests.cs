using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;
using static MackySoft.Ucli.Tests.UnityUcliPluginLocatorTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class UnityUcliPluginLocatorCacheTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenMarkerIsResolved_WritesRelativePathCache ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "writes-cache");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var unityProjectRoot = AbsolutePath.Parse(unityProjectPath);
        await WriteMarkerAsync(
            scope,
            RootRelativePath.Parse(Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity")));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var result = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        var cache = await ReadCacheAsync(unityProjectRoot);
        Assert.Equal(
            "Assets/MackySoft/MackySoft.Ucli.Unity/ucli-plugin.json",
            cache.ProjectRelativeMarkerPath);
        Assert.Equal(UnityUcliPluginMarkerContract.ExpectedPluginId, cache.PluginId);
        Assert.Equal(UnityUcliPluginMarkerContract.ExpectedProtocolVersion, cache.ProtocolVersion);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenValidCacheExists_SkipsFallbackScan ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "cache-hit");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var unityProjectRoot = AbsolutePath.Parse(unityProjectPath);
        await WriteMarkerAsync(
            scope,
            RootRelativePath.Parse(Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity")));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var firstResult = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);

        await scope.WriteFileAsync(
            Path.Combine("UnityProject", "Packages", "com.example.invalid", UnityUcliPluginMarkerContract.MarkerFileName),
            "{ invalid");

        var secondResult = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);

        Assert.True(secondResult.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, secondResult.Status);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenValidCacheExistsAndAdditionalValidMarkerIsAdded_ReturnsCachedMarker ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "cache-hit-valid-duplicate");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var unityProjectRoot = AbsolutePath.Parse(unityProjectPath);
        await WriteMarkerAsync(
            scope,
            RootRelativePath.Parse(Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity")));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var firstResult = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);

        await WriteMarkerAsync(
            scope,
            RootRelativePath.Parse(Path.Combine("UnityProject", "Assets", "ThirdParty", "UcliCopy")));

        var secondResult = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);

        Assert.True(secondResult.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, secondResult.Status);
        Assert.Equal(
            ResolveMarkerPath(
                unityProjectRoot,
                RootRelativePath.Parse(Path.Combine("Assets", "MackySoft", "MackySoft.Ucli.Unity"))),
            secondResult.MarkerPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenCachedMarkerPathIsStale_RebuildsCacheFromScan ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "stale-cache");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var unityProjectRoot = AbsolutePath.Parse(unityProjectPath);
        await WriteMarkerAsync(
            scope,
            RootRelativePath.Parse(Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity")));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = CreateLocator(observedCacheStore.CacheStore);
        var firstCacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var firstResult = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);
        await TestAwaiter.WaitAsync(firstCacheWriteTask, "Plugin marker initial cache write", SignalWaitTimeout);

        File.Delete(ResolveMarkerPath(
            unityProjectRoot,
            RootRelativePath.Parse(Path.Combine("Assets", "MackySoft", "MackySoft.Ucli.Unity"))).Value);
        await WriteMarkerAsync(
            scope,
            RootRelativePath.Parse(Path.Combine("UnityProject", "Packages", "com.mackysoft.ucli.unity")));
        var secondCacheWriteTask = observedCacheStore.ExpectWriteAsync();

        var secondResult = await locator.LocateAsync(unityProjectRoot, CancellationToken.None);

        Assert.True(secondResult.IsSuccess);
        await TestAwaiter.WaitAsync(secondCacheWriteTask, "Plugin marker rebuilt cache write", SignalWaitTimeout);
        var cache = await ReadCacheAsync(unityProjectRoot);
        Assert.Equal(
            "Packages/com.mackysoft.ucli.unity/ucli-plugin.json",
            cache.ProjectRelativeMarkerPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Locate_WhenCallerIsCanceledDuringBestEffortCacheWrite_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "cache-write-cancel");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var unityProjectRoot = AbsolutePath.Parse(unityProjectPath);
        await WriteMarkerAsync(
            scope,
            RootRelativePath.Parse(Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity")));

        var writeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cacheStore = new UnityUcliPluginMarkerCacheStore(
            static (path, cancellationToken) => FileUtilities.ReadAllTextOrNullAsync(path, cancellationToken),
            async (path, contents, cancellationToken) =>
            {
                writeStarted.TrySetResult(true);
                await releaseWrite.Task.ConfigureAwait(false);
            },
            static path => FileUtilities.DeleteIfExists(path));
        var locator = CreateLocator(cacheStore);
        using var cancellationTokenSource = new CancellationTokenSource();

        var locateTask = locator.LocateAsync(
            unityProjectRoot,
            cancellationTokenSource.Token).AsTask();
        await TestAwaiter.WaitAsync(writeStarted.Task, "Plugin marker cache write start", SignalWaitTimeout);
        cancellationTokenSource.Cancel();
        releaseWrite.TrySetResult(true);

        var result = await TestAwaiter.WaitAsync(locateTask, "Plugin marker locate after cancellation", SignalWaitTimeout);

        Assert.True(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
    }
}
