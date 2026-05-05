using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Init.Adapters;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class FileInitTemplateStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenTemplatesDoNotExist_CreatesConfigAndGitIgnore ()
    {
        using var scope = TestDirectories.CreateTempScope("init-service", "success");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        using var currentDirectoryScope = new CurrentDirectoryScope(workingDirectoryPath);
        var configStore = new StubConfigStore(saveHandler: static (storageRoot, _, _) =>
        {
            var configPath = UcliStoragePathResolver.ResolveConfigPath(storageRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(configPath, "{}");
            return ValueTask.FromResult(UcliConfigSaveResult.Success());
        });
        var store = new FileInitTemplateStore(configStore);

        var result = await store.WriteAsync(UcliConfig.CreateDefault(), false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var output = Assert.IsType<InitExecutionOutput>(result.Output);
        var expectedStorageRoot = UcliStoragePathResolver.ResolveStorageRoot(workingDirectoryPath);
        var expectedConfigPath = UcliStoragePathResolver.ResolveConfigPath(expectedStorageRoot);
        var expectedGitIgnorePath = Path.Combine(
            UcliStoragePathResolver.ResolveUcliDirectoryPath(expectedStorageRoot),
            UcliStoragePathNames.GitIgnoreFileName);

        FileSystemAssert.ForPath(configStore.LastStorageRoot!).EqualsNormalized(expectedStorageRoot);
        FileSystemAssert.ForPath(output.ConfigPath).EqualsNormalized(expectedConfigPath).Exists();
        FileSystemAssert.ForPath(output.GitIgnorePath).EqualsNormalized(expectedGitIgnorePath).Exists();
        Assert.Equal(
            UcliLocalStorageBootstrapper.LocalDirectoryIgnoreEntry + Environment.NewLine,
            File.ReadAllText(expectedGitIgnorePath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenTemplatesAlreadyExistWithoutForce_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("init-service", "existing-without-force");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        using var currentDirectoryScope = new CurrentDirectoryScope(workingDirectoryPath);
        var configPath = scope.WriteFile(Path.Combine("workspace", ".ucli", "config.json"), "{}");
        var gitIgnorePath = scope.WriteFile(Path.Combine("workspace", ".ucli", ".gitignore"), ".local/");
        var configStore = new StubConfigStore(saveHandler: static (_, _, _) => throw new InvalidOperationException("Save should not be called."));
        var store = new FileInitTemplateStore(configStore);

        var result = await store.WriteAsync(UcliConfig.CreateDefault(), false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains(configPath, error.Message, StringComparison.Ordinal);
        Assert.Contains(gitIgnorePath, error.Message, StringComparison.Ordinal);
        Assert.Equal(0, configStore.SaveCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenUcliDirectoryPathPointsToFile_ReturnsInternalError ()
    {
        using var scope = TestDirectories.CreateTempScope("init-service", "ucli-dir-file");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        using var currentDirectoryScope = new CurrentDirectoryScope(workingDirectoryPath);
        scope.WriteFile(Path.Combine("workspace", ".ucli"), "occupied");
        var configStore = new StubConfigStore(saveHandler: static (_, _, _) => throw new InvalidOperationException("Save should not be called."));
        var store = new FileInitTemplateStore(configStore);

        var result = await store.WriteAsync(UcliConfig.CreateDefault(), true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to create .ucli directory", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, configStore.SaveCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenConfigSaveFails_ReturnsSaveError ()
    {
        using var scope = TestDirectories.CreateTempScope("init-service", "config-save-failure");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        using var currentDirectoryScope = new CurrentDirectoryScope(workingDirectoryPath);
        var configStore = new StubConfigStore(saveHandler: static (_, _, _) => ValueTask.FromResult(
            UcliConfigSaveResult.Failure(ExecutionError.InternalError("config save failed."))));
        var store = new FileInitTemplateStore(configStore);

        var result = await store.WriteAsync(UcliConfig.CreateDefault(), false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("config save failed.", error.Message);
        Assert.Equal(1, configStore.SaveCallCount);
    }

    private sealed class CurrentDirectoryScope : IDisposable
    {
        private readonly string originalCurrentDirectory;

        public CurrentDirectoryScope (string currentDirectoryPath)
        {
            originalCurrentDirectory = Environment.CurrentDirectory;
            Environment.CurrentDirectory = currentDirectoryPath;
        }

        public void Dispose ()
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
        }
    }

    private sealed class StubConfigStore : IUcliConfigStore
    {
        private readonly Func<string, UcliConfig, CancellationToken, ValueTask<UcliConfigSaveResult>> saveHandler;

        public StubConfigStore (Func<string, UcliConfig, CancellationToken, ValueTask<UcliConfigSaveResult>> saveHandler)
        {
            this.saveHandler = saveHandler ?? throw new ArgumentNullException(nameof(saveHandler));
        }

        public int SaveCallCount { get; private set; }

        public string? LastStorageRoot { get; private set; }

        public UcliConfig? LastConfig { get; private set; }

        public string GetConfigPath (string storageRoot)
        {
            return UcliStoragePathResolver.ResolveConfigPath(storageRoot);
        }

        public ValueTask<UcliConfigLoadResult> Load (
            string storageRoot,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<UcliConfigSaveResult> Save (
            string storageRoot,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            LastStorageRoot = storageRoot;
            LastConfig = config;
            return saveHandler(storageRoot, config, cancellationToken);
        }
    }
}
