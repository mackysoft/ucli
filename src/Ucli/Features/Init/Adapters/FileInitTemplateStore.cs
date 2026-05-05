using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Features.Init.UseCases.Init;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Init.Adapters;

/// <summary> Persists init template files through the local filesystem. </summary>
internal sealed class FileInitTemplateStore : IInitTemplateStore
{
    private readonly IUcliConfigStore configStore;

    /// <summary> Initializes a new instance of the <see cref="FileInitTemplateStore" /> class. </summary>
    /// <param name="configStore"> The config store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="configStore" /> is <see langword="null" />. </exception>
    public FileInitTemplateStore (IUcliConfigStore configStore)
    {
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
    }

    /// <inheritdoc />
    public async ValueTask<InitExecutionResult> WriteAsync (
        UcliConfig config,
        bool force,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        string currentDirectoryPath;
        try
        {
            currentDirectoryPath = Path.GetFullPath(Environment.CurrentDirectory);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return InitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Current working directory path is invalid: {Environment.CurrentDirectory}. {ex.Message}"));
        }

        var repositoryRoot = UcliStoragePathResolver.ResolveStorageRoot(currentDirectoryPath);
        var ucliDirectoryPath = UcliStoragePathResolver.ResolveUcliDirectoryPath(repositoryRoot);
        var configPath = UcliStoragePathResolver.ResolveConfigPath(repositoryRoot);
        var gitIgnorePath = Path.Combine(ucliDirectoryPath, UcliStoragePathNames.GitIgnoreFileName);
        var existingPaths = CollectExistingTemplatePaths(configPath, gitIgnorePath);

        if (!force && existingPaths.Count > 0)
        {
            var joinedPaths = string.Join(", ", existingPaths);
            return InitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Initialization failed because template files already exist. Use --force to overwrite: {joinedPaths}"));
        }

        try
        {
            Directory.CreateDirectory(ucliDirectoryPath);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return InitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI directory path is invalid: {ucliDirectoryPath}. {ex.Message}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return InitExecutionResult.Failure(ExecutionError.InternalError(
                $"Failed to create .ucli directory: {ucliDirectoryPath}. {ex.Message}"));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var configSaveResult = await configStore.Save(repositoryRoot, config, cancellationToken).ConfigureAwait(false);
        if (!configSaveResult.IsSuccess)
        {
            return InitExecutionResult.Failure(configSaveResult.Error!);
        }

        try
        {
            await File.WriteAllTextAsync(
                    gitIgnorePath,
                    UcliLocalStorageBootstrapper.LocalDirectoryIgnoreEntry + Environment.NewLine,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return InitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Git ignore path is invalid: {gitIgnorePath}. {ex.Message}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return InitExecutionResult.Failure(ExecutionError.InternalError(
                $"Failed to write git ignore file: {gitIgnorePath}. {ex.Message}"));
        }

        var output = new InitExecutionOutput(
            ConfigPath: configPath,
            GitIgnorePath: gitIgnorePath);
        return InitExecutionResult.Success(output);
    }

    /// <summary> Collects existing init-template files for precondition checks. </summary>
    /// <param name="configPath"> The config file path. </param>
    /// <param name="gitIgnorePath"> The git-ignore file path. </param>
    /// <returns> Existing file paths that would be overwritten. </returns>
    private static List<string> CollectExistingTemplatePaths (
        string configPath,
        string gitIgnorePath)
    {
        var existingPaths = new List<string>(2);
        if (File.Exists(configPath))
        {
            existingPaths.Add(configPath);
        }

        if (File.Exists(gitIgnorePath))
        {
            existingPaths.Add(gitIgnorePath);
        }

        return existingPaths;
    }

    /// <summary> Determines whether an exception indicates a filesystem I/O failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when it is an I/O failure; otherwise <see langword="false" />. </returns>
    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }
}
