using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Init;

/// <summary> Implements init flow that generates the <c>.ucli</c> template files. </summary>
internal sealed class InitService : IInitService
{
    private const string UcliDirectoryName = ".ucli";
    private const string LocalDirectoryName = "local";
    private const string FingerprintsDirectoryName = "fingerprints";
    private const string ConfigFileName = "config.json";
    private const string GitIgnoreFileName = ".gitignore";
    private const string GitIgnoreContents = "local/";

    private readonly IUcliConfigStore configStore;

    /// <summary> Initializes a new instance of the <see cref="InitService" /> class. </summary>
    /// <param name="configStore"> The config store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="configStore" /> is <see langword="null" />. </exception>
    public InitService (IUcliConfigStore configStore)
    {
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
    }

    /// <summary> Executes initialization to create or overwrite <c>.ucli/config.json</c> and <c>.ucli/.gitignore</c>. </summary>
    /// <param name="force"> Whether existing files can be overwritten. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the init execution result that contains generated file paths on success or a structured error on failure. </returns>
    public async ValueTask<InitExecutionResult> Execute (
        bool force,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string currentDirectoryPath;
        try
        {
            currentDirectoryPath = Path.GetFullPath(Environment.CurrentDirectory);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
        {
            return InitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Current working directory path is invalid: {Environment.CurrentDirectory}. {ex.Message}"));
        }

        var repositoryRoot = UcliStorageRootPathResolver.Resolve(currentDirectoryPath);
        var ucliDirectoryPath = Path.Combine(repositoryRoot, UcliDirectoryName);
        var localDirectoryPath = Path.Combine(ucliDirectoryPath, LocalDirectoryName);
        var fingerprintsDirectoryPath = Path.Combine(localDirectoryPath, FingerprintsDirectoryName);
        var configPath = Path.Combine(ucliDirectoryPath, ConfigFileName);
        var gitIgnorePath = Path.Combine(ucliDirectoryPath, GitIgnoreFileName);
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
            Directory.CreateDirectory(localDirectoryPath);
            Directory.CreateDirectory(fingerprintsDirectoryPath);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
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

        var defaultConfig = UcliConfig.CreateDefault();
        var configSaveResult = await configStore.Save(repositoryRoot, defaultConfig, cancellationToken).ConfigureAwait(false);
        if (!configSaveResult.IsSuccess)
        {
            return InitExecutionResult.Failure(configSaveResult.Error!);
        }

        try
        {
            await File.WriteAllTextAsync(gitIgnorePath, GitIgnoreContents + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
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

    /// <summary> Determines whether an exception indicates invalid path formatting. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when it is a path-format exception; otherwise <see langword="false" />. </returns>
    private static bool IsPathFormatException (Exception exception)
    {
        return exception is ArgumentException
            or NotSupportedException
            or PathTooLongException;
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