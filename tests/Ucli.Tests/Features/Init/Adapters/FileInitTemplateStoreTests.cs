using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Init.Adapters;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Configuration;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class FileInitTemplateStoreTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenTemplatesDoNotExist_CreatesConfigAndGitIgnore ()
    {
        using var scope = TestDirectories.CreateTempScope("init-service", "success");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        using var currentDirectoryScope = new CurrentDirectoryScope(workingDirectoryPath);
        var configStore = new RecordingUcliConfigStore
        {
            SaveHandler = static (storageRoot, _, _) =>
            {
                var configPath = UcliStoragePathResolver.ResolveConfigPath(storageRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(configPath.Value)!);
                File.WriteAllText(configPath.Value, "{}");
                return ValueTask.FromResult(UcliConfigSaveResult.Success());
            },
        };
        var store = new FileInitTemplateStore(configStore);
        var config = UcliConfig.CreateDefault();

        var result = await store.WriteAsync(config, false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var output = Assert.IsType<InitExecutionOutput>(result.Output);
        var expectedStorageRoot = UcliStoragePathResolver.ResolveStorageRoot(
            AbsolutePath.Parse(Environment.CurrentDirectory));
        var expectedConfigPath = UcliStoragePathResolver.ResolveConfigPath(expectedStorageRoot);
        var expectedGitIgnorePath = Path.Combine(
            UcliStoragePathResolver.ResolveUcliDirectoryPath(expectedStorageRoot).Value,
            UcliStoragePathNames.GitIgnoreFileName);

        UcliConfigStoreAssert.ConfigSavedFor(configStore, expectedStorageRoot, config);
        FileSystemAssert.ForPath(output.ConfigPath).EqualsNormalized(expectedConfigPath.Value).Exists();
        FileSystemAssert.ForPath(output.GitIgnorePath).EqualsNormalized(expectedGitIgnorePath).Exists();
        Assert.Equal(
            UcliLocalStorageBootstrapper.LocalDirectoryIgnoreEntry + Environment.NewLine,
            File.ReadAllText(expectedGitIgnorePath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenTemplatesAlreadyExistWithoutForce_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("init-service", "existing-without-force");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        using var currentDirectoryScope = new CurrentDirectoryScope(workingDirectoryPath);
        var configPath = scope.WriteFile(Path.Combine("workspace", ".ucli", "config.json"), "{}");
        var gitIgnorePath = scope.WriteFile(Path.Combine("workspace", ".ucli", ".gitignore"), ".local/");
        var configStore = new RecordingUcliConfigStore
        {
            SaveHandler = static (_, _, _) => throw new InvalidOperationException("Save should not be called."),
        };
        var store = new FileInitTemplateStore(configStore);

        var result = await store.WriteAsync(UcliConfig.CreateDefault(), false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains(configPath, error.Message, StringComparison.Ordinal);
        Assert.Contains(gitIgnorePath, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenUcliDirectoryPathPointsToFile_ReturnsInternalError ()
    {
        using var scope = TestDirectories.CreateTempScope("init-service", "ucli-dir-file");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        using var currentDirectoryScope = new CurrentDirectoryScope(workingDirectoryPath);
        scope.WriteFile(Path.Combine("workspace", ".ucli"), "occupied");
        var configStore = new RecordingUcliConfigStore
        {
            SaveHandler = static (_, _, _) => throw new InvalidOperationException("Save should not be called."),
        };
        var store = new FileInitTemplateStore(configStore);

        var result = await store.WriteAsync(UcliConfig.CreateDefault(), true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to create .ucli directory", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenConfigSaveFails_ReturnsSaveError ()
    {
        using var scope = TestDirectories.CreateTempScope("init-service", "config-save-failure");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        using var currentDirectoryScope = new CurrentDirectoryScope(workingDirectoryPath);
        var configStore = new RecordingUcliConfigStore
        {
            SaveHandler = static (_, _, _) => ValueTask.FromResult(
                UcliConfigSaveResult.Failure(ExecutionError.InternalError("config save failed."))),
        };
        var store = new FileInitTemplateStore(configStore);
        var config = UcliConfig.CreateDefault();

        var result = await store.WriteAsync(config, false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("config save failed.", error.Message);
        UcliConfigStoreAssert.ConfigSavedFor(
            configStore,
            UcliStoragePathResolver.ResolveStorageRoot(AbsolutePath.Parse(Environment.CurrentDirectory)),
            config);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenConfigSaveReturnsDiagnostics_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("init-service", "config-save-diagnostics");
        var workingDirectoryPath = scope.CreateDirectory("workspace");
        using var currentDirectoryScope = new CurrentDirectoryScope(workingDirectoryPath);
        var configStore = new RecordingUcliConfigStore
        {
            SaveHandler = static (_, _, _) => ValueTask.FromResult(
                UcliConfigSaveResult.Failure(
                [
                    UcliConfigDiagnostic.Create(
                        "config.save.invalidTimeout",
                        "ipcDefaultTimeoutMilliseconds",
                        "config.json",
                        "Config timeout is invalid."),
                    UcliConfigDiagnostic.Create(
                        "config.save.invalidRegexPattern",
                        "operationAllowlist[0]",
                        "config.json",
                        "Config allowlist pattern is invalid."),
                ])),
        };
        var store = new FileInitTemplateStore(configStore);
        var config = UcliConfig.CreateDefault();

        var result = await store.WriteAsync(config, false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("Config timeout is invalid.", error.Message, StringComparison.Ordinal);
        Assert.Contains("Config allowlist pattern is invalid.", error.Message, StringComparison.Ordinal);
        var expectedStorageRoot = UcliStoragePathResolver.ResolveStorageRoot(
            AbsolutePath.Parse(Environment.CurrentDirectory));
        var expectedGitIgnorePath = Path.Combine(
            UcliStoragePathResolver.ResolveUcliDirectoryPath(expectedStorageRoot).Value,
            UcliStoragePathNames.GitIgnoreFileName);
        Assert.False(File.Exists(expectedGitIgnorePath));
        UcliConfigStoreAssert.ConfigSavedFor(configStore, expectedStorageRoot, config);
    }
}
