using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Project;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;

namespace MackySoft.Ucli.Tests;

public sealed class UnityUcliPluginLocatorTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenMarkerExistsUnderAssets_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "assets-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = new UnityUcliPluginLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWrite();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.NotNull(result.MarkerPath);
        Assert.EndsWith(
            Path.Combine("Assets", "MackySoft", "MackySoft.Ucli.Unity", UnityUcliPluginLocator.MarkerFileName),
            result.MarkerPath,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenMarkerExistsUnderPackages_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "packages-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Packages", "com.mackysoft.ucli.unity"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = new UnityUcliPluginLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWrite();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.NotNull(result.MarkerPath);
        Assert.EndsWith(
            Path.Combine("Packages", "com.mackysoft.ucli.unity", UnityUcliPluginLocator.MarkerFileName),
            result.MarkerPath,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenMarkerExistsUnderAssetsPackages_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "assets-packages-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "Packages", "com.mackysoft.ucli.unity.1.0.0"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = new UnityUcliPluginLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWrite();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.NotNull(result.MarkerPath);
        Assert.EndsWith(
            Path.Combine("Assets", "Packages", "com.mackysoft.ucli.unity.1.0.0", UnityUcliPluginLocator.MarkerFileName),
            result.MarkerPath,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenMarkerExistsUnderAssetsPackagesWithPascalPackageId_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "assets-packages-pascal-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "Packages", "MackySoft.Ucli.Unity.1.0.0"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = new UnityUcliPluginLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWrite();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
        Assert.NotNull(result.MarkerPath);
        Assert.EndsWith(
            Path.Combine("Assets", "Packages", "MackySoft.Ucli.Unity.1.0.0", UnityUcliPluginLocator.MarkerFileName),
            result.MarkerPath,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenMarkerIsResolved_WritesRelativePathCache ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "writes-cache");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = new UnityUcliPluginLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWrite();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);
        var cache = await ReadCache(scope, unityProjectPath);
        Assert.Equal(
            "Assets/MackySoft/MackySoft.Ucli.Unity/ucli-plugin.json",
            cache.ProjectRelativeMarkerPath);
        Assert.Equal(UnityUcliPluginLocator.ExpectedPluginId, cache.PluginId);
        Assert.Equal(UnityUcliPluginLocator.ExpectedProtocolVersion, cache.ProtocolVersion);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenValidCacheExists_SkipsFallbackScan ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "cache-hit");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = new UnityUcliPluginLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWrite();

        var firstResult = await locator.Locate(unityProjectPath, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);

        await scope.WriteFileAsync(
            Path.Combine("UnityProject", "Packages", "com.example.invalid", UnityUcliPluginLocator.MarkerFileName),
            "{ invalid");

        var secondResult = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.True(secondResult.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, secondResult.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenValidCacheExistsAndAdditionalValidMarkerIsAdded_ReturnsCachedMarker ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "cache-hit-valid-duplicate");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = new UnityUcliPluginLocator(observedCacheStore.CacheStore);
        var cacheWriteTask = observedCacheStore.ExpectWrite();

        var firstResult = await locator.Locate(unityProjectPath, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);
        await TestAwaiter.WaitAsync(cacheWriteTask, "Plugin marker cache write", SignalWaitTimeout);

        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "ThirdParty", "UcliCopy"));

        var secondResult = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.True(secondResult.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, secondResult.Status);
        Assert.NotNull(secondResult.MarkerPath);
        Assert.EndsWith(
            Path.Combine("Assets", "MackySoft", "MackySoft.Ucli.Unity", UnityUcliPluginLocator.MarkerFileName),
            secondResult.MarkerPath,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenCachedMarkerPathIsStale_RebuildsCacheFromScan ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "stale-cache");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        var observedCacheStore = new ObservedPluginMarkerCacheStore();
        var locator = new UnityUcliPluginLocator(observedCacheStore.CacheStore);
        var firstCacheWriteTask = observedCacheStore.ExpectWrite();

        var firstResult = await locator.Locate(unityProjectPath, CancellationToken.None);
        Assert.True(firstResult.IsSuccess);
        await TestAwaiter.WaitAsync(firstCacheWriteTask, "Plugin marker initial cache write", SignalWaitTimeout);

        File.Delete(Path.Combine(
            unityProjectPath,
            "Assets",
            "MackySoft",
            "MackySoft.Ucli.Unity",
            UnityUcliPluginLocator.MarkerFileName));
        await WriteMarker(scope, Path.Combine("UnityProject", "Packages", "com.mackysoft.ucli.unity"));
        var secondCacheWriteTask = observedCacheStore.ExpectWrite();

        var secondResult = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.True(secondResult.IsSuccess);
        await TestAwaiter.WaitAsync(secondCacheWriteTask, "Plugin marker rebuilt cache write", SignalWaitTimeout);
        var cache = await ReadCache(scope, unityProjectPath);
        Assert.Equal(
            "Packages/com.mackysoft.ucli.unity/ucli-plugin.json",
            cache.ProjectRelativeMarkerPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenCacheIsMissingAndStandardAndNonStandardMarkersExist_ReturnsMultipleFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "multiple-found-standard-and-nonstandard");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "ThirdParty", "UcliCopy"));
        var locator = new UnityUcliPluginLocator();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.MultipleFound, result.Status);
        Assert.Equal(2, result.MarkerPaths.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenMarkerDoesNotExist_ReturnsNotFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "not-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var locator = new UnityUcliPluginLocator();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.NotFound, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("does not contain the uCLI Unity plugin", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenMarkerJsonIsInvalid_ReturnsInvalidMarker ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "invalid-json");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await scope.WriteFileAsync(
            Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity", UnityUcliPluginLocator.MarkerFileName),
            "{ invalid");
        var locator = new UnityUcliPluginLocator();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.InvalidMarker, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("could not be parsed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenPluginIdDoesNotMatch_ReturnsInvalidMarker ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "invalid-plugin-id");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(
            scope,
            Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"),
            """
            {
              "pluginId": "com.example.other",
              "protocolVersion": 1
            }
            """);
        var locator = new UnityUcliPluginLocator();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.InvalidMarker, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("pluginId must be", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenProtocolVersionDoesNotMatch_ReturnsInvalidMarker ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "invalid-protocol-version");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(
            scope,
            Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"),
            """
            {
              "pluginId": "com.mackysoft.ucli.unity",
              "protocolVersion": 2
            }
            """);
        var locator = new UnityUcliPluginLocator();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.InvalidMarker, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("protocolVersion must be", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenCallerIsCanceledDuringBestEffortCacheWrite_ReturnsFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "cache-write-cancel");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));

        var writeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cacheStore = new UnityUcliPluginMarkerCacheStore(
            static (path, cancellationToken) => FileUtilities.ReadAllTextOrNull(path, cancellationToken),
            async (path, contents, cancellationToken) =>
            {
                writeStarted.TrySetResult(true);
                await releaseWrite.Task.ConfigureAwait(false);
            },
            static path => FileUtilities.DeleteIfExists(path));
        var locator = new UnityUcliPluginLocator(cacheStore);
        using var cancellationTokenSource = new CancellationTokenSource();

        var locateTask = locator.Locate(unityProjectPath, cancellationTokenSource.Token).AsTask();
        await TestAwaiter.WaitAsync(writeStarted.Task, "Plugin marker cache write start", SignalWaitTimeout);
        cancellationTokenSource.Cancel();
        releaseWrite.TrySetResult(true);

        var result = await TestAwaiter.WaitAsync(locateTask, "Plugin marker locate after cancellation", SignalWaitTimeout);

        Assert.True(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.Found, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Locate_WhenMultipleMarkersExist_ReturnsMultipleFound ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ucli-plugin-locator", "multiple-found");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        await WriteMarker(scope, Path.Combine("UnityProject", "Assets", "MackySoft", "MackySoft.Ucli.Unity"));
        await WriteMarker(scope, Path.Combine("UnityProject", "Packages", "com.mackysoft.ucli.unity"));
        var locator = new UnityUcliPluginLocator();

        var result = await locator.Locate(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityUcliPluginLocateStatus.MultipleFound, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(2, result.MarkerPaths.Count);
        Assert.Contains("Multiple uCLI Unity plugin markers were found", error.Message, StringComparison.Ordinal);
    }

    private static Task WriteMarker (
        TestDirectoryScope scope,
        string markerDirectoryRelativePath,
        string? contents = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerDirectoryRelativePath);

        return scope.WriteFileAsync(
            Path.Combine(markerDirectoryRelativePath, UnityUcliPluginLocator.MarkerFileName),
            contents
            ?? """
               {
                 "pluginId": "com.mackysoft.ucli.unity",
                 "protocolVersion": 1
               }
               """);
    }

    private static async Task<UnityUcliPluginMarkerCache> ReadCache (
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

    private sealed class ObservedPluginMarkerCacheStore
    {
        private TaskCompletionSource<bool> nextWrite =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ObservedPluginMarkerCacheStore ()
        {
            CacheStore = new UnityUcliPluginMarkerCacheStore(
                static (path, cancellationToken) => FileUtilities.ReadAllTextOrNull(path, cancellationToken),
                WriteAllTextAtomically,
                static path => FileUtilities.DeleteIfExists(path));
        }

        public UnityUcliPluginMarkerCacheStore CacheStore { get; }

        public Task ExpectWrite ()
        {
            nextWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return nextWrite.Task;
        }

        private async ValueTask WriteAllTextAtomically (
            string path,
            string contents,
            CancellationToken cancellationToken)
        {
            try
            {
                await FileUtilities.WriteAllTextAtomically(path, contents, cancellationToken);
            }
            finally
            {
                nextWrite.TrySetResult(true);
            }
        }
    }
}