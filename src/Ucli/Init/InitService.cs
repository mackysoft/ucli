using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Init;

/// <summary> Implements init flow that generates the <c>.ucli</c> template files. </summary>
internal sealed class InitService : IInitService
{
    private const string UcliDirectoryName = ".ucli";
    private const string GitIgnoreFileName = ".gitignore";
    private const string GitIgnoreContents = "local/";

    private readonly IUnityProjectResolver unityProjectResolver;
    private readonly IInitStatusContextResolver contextResolver;
    private readonly IUcliConfigStore configStore;

    /// <summary> Initializes a new instance of the <see cref="InitService" /> class. </summary>
    /// <param name="unityProjectResolver"> The UnityProject resolver dependency. </param>
    /// <param name="contextResolver"> The init/status context resolver dependency. </param>
    /// <param name="configStore"> The config store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProjectResolver" />, <paramref name="contextResolver" />, or <paramref name="configStore" /> is <see langword="null" />. </exception>
    public InitService (
        IUnityProjectResolver unityProjectResolver,
        IInitStatusContextResolver contextResolver,
        IUcliConfigStore configStore)
    {
        this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
        this.contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
    }

    /// <summary> Executes initialization to create or overwrite <c>.ucli/config.json</c> and <c>.ucli/.gitignore</c>. </summary>
    /// <param name="force"> Whether existing files can be overwritten. </param>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. When <see langword="null" />, empty, or whitespace, the current working directory is used. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the init execution result that contains generated file paths on success or a structured error on failure. </returns>
    public async ValueTask<InitExecutionResult> Execute (
        bool force,
        string? projectPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var unityProjectResult = unityProjectResolver.Resolve(projectPath);
        if (!unityProjectResult.IsSuccess)
        {
            return InitExecutionResult.Failure(unityProjectResult.Error!);
        }

        var unityProjectContext = unityProjectResult.Context!;
        var unityProjectRoot = unityProjectContext.UnityProjectRoot;
        var ucliDirectoryPath = Path.Combine(unityProjectRoot, UcliDirectoryName);
        var configPath = unityProjectContext.ConfigPath;
        var gitIgnorePath = Path.Combine(ucliDirectoryPath, GitIgnoreFileName);
        var existingPaths = CollectExistingTemplatePaths(configPath, gitIgnorePath);

        if (!force && existingPaths.Count > 0)
        {
            var joinedPaths = string.Join(", ", existingPaths);
            return InitExecutionResult.Failure(CreateInvalidArgument(
                $"Initialization failed because template files already exist. Use --force to overwrite: {joinedPaths}"));
        }

        if (!force)
        {
            // NOTE:
            // Keep init and status using the same context pipeline.
            // This ensures config parse/validation behavior stays consistent across command foundations.
            var contextResult = await contextResolver.Resolve(projectPath, cancellationToken).ConfigureAwait(false);
            if (!contextResult.IsSuccess)
            {
                return InitExecutionResult.Failure(contextResult.Error!);
            }
        }

        try
        {
            Directory.CreateDirectory(ucliDirectoryPath);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
        {
            return InitExecutionResult.Failure(CreateInvalidArgument(
                $"UnityProject path is invalid: {unityProjectRoot}. {ex.Message}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return InitExecutionResult.Failure(CreateInternalError(
                $"Failed to create .ucli directory: {ucliDirectoryPath}. {ex.Message}"));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var defaultConfig = UcliConfig.CreateDefault();
        var configSaveResult = await configStore.Save(unityProjectRoot, defaultConfig, cancellationToken).ConfigureAwait(false);
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
            return InitExecutionResult.Failure(CreateInvalidArgument(
                $"Git ignore path is invalid: {gitIgnorePath}. {ex.Message}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return InitExecutionResult.Failure(CreateInternalError(
                $"Failed to write git ignore file: {gitIgnorePath}. {ex.Message}"));
        }

        var output = new InitExecutionOutput(
            ProjectPath: unityProjectRoot,
            ProjectFingerprint: unityProjectContext.ProjectFingerprint,
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

    /// <summary> Creates an invalid-argument error value. </summary>
    /// <param name="message"> The error message. </param>
    /// <returns> The structured invalid-argument error. </returns>
    private static ExecutionError CreateInvalidArgument (string message)
    {
        return new ExecutionError(ExecutionErrorKind.InvalidArgument, message);
    }

    /// <summary> Creates an internal-error value. </summary>
    /// <param name="message"> The error message. </param>
    /// <returns> The structured internal-error value. </returns>
    private static ExecutionError CreateInternalError (string message)
    {
        return new ExecutionError(ExecutionErrorKind.InternalError, message);
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