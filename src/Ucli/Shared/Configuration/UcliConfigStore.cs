using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Shared.Configuration;

/// <summary> Provides filesystem-backed access to <c>.ucli/config.json</c>. </summary>
internal sealed class UcliConfigStore : IUcliConfigStore
{
    private const string InvalidJsonCode = "config.json.invalid";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly UcliConfigCompiler compiler;

    /// <summary> Initializes a new instance of the <see cref="UcliConfigStore" /> class. </summary>
    /// <param name="compiler"> The config compiler dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="compiler" /> is <see langword="null" />. </exception>
    public UcliConfigStore (UcliConfigCompiler compiler)
    {
        this.compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    /// <summary> Resolves the absolute path to <c>.ucli/config.json</c> for a storage root. </summary>
    /// <param name="storageRoot">
    /// <para> The storage-root path used as the base directory. </para>
    /// <para> Must not be <see langword="null" />. </para>
    /// </param>
    /// <returns> The absolute config path. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="storageRoot" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> contains invalid path characters. </exception>
    /// <exception cref="NotSupportedException"> Thrown when <paramref name="storageRoot" /> uses an unsupported path format. </exception>
    /// <exception cref="PathTooLongException"> Thrown when <paramref name="storageRoot" /> exceeds platform path limits. </exception>
    public string GetConfigPath (string storageRoot)
    {
        return UcliStoragePathResolver.ResolveConfigPath(storageRoot);
    }

    /// <summary> Loads configuration values for a storage root. </summary>
    /// <param name="storageRoot">
    /// <para> The storage-root path from command context. </para>
    /// <para> <see langword="null" />, empty, and whitespace values return an invalid-argument result. </para>
    /// </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the config-load result. When <c>.ucli/config.json</c> does not exist, default config values are returned with <see cref="ConfigSource.Default" />. </returns>
    public async ValueTask<UcliConfigLoadResult> LoadAsync (
        string storageRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            return UcliConfigLoadResult.Failure(ExecutionError.InvalidArgument("Storage root path must not be empty."));
        }

        string configPath;
        try
        {
            configPath = GetConfigPath(storageRoot);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return UcliConfigLoadResult.Failure(ExecutionError.InvalidArgument(
                $"Storage root path is invalid: {storageRoot}"));
        }

        if (!File.Exists(configPath))
        {
            return UcliConfigLoadResult.Success(UcliConfig.CreateDefault(), ConfigSource.Default);
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return UcliConfigLoadResult.Failure(ExecutionError.InvalidArgument(
                $"Config path is invalid: {configPath}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return UcliConfigLoadResult.Failure(ExecutionError.InternalError(
                $"Failed to read config file: {configPath}. {ex.Message}"));
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(json);
            var compileResult = compiler.Compile(jsonDocument.RootElement, configPath);
            if (!compileResult.IsSuccess)
            {
                return UcliConfigLoadResult.Failure(compileResult.Diagnostics);
            }

            return UcliConfigLoadResult.Success(compileResult.Config!, ConfigSource.File);
        }
        catch (JsonException ex)
        {
            return UcliConfigLoadResult.Failure(
            [
                UcliConfigDiagnostic.Create(
                    InvalidJsonCode,
                    propertyPath: null,
                    sourcePath: configPath,
                    $"Config JSON is invalid: {ex.Message}"),
            ]);
        }
    }

    /// <summary> Saves configuration values to <c>.ucli/config.json</c>. </summary>
    /// <param name="storageRoot">
    /// <para> The storage-root path from command context. </para>
    /// <para> <see langword="null" />, empty, and whitespace values return an invalid-argument result. </para>
    /// </param>
    /// <param name="config"> The config values to persist. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the config-save result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public async ValueTask<UcliConfigSaveResult> SaveAsync (
        string storageRoot,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            return UcliConfigSaveResult.Failure(ExecutionError.InvalidArgument("Storage root path must not be empty."));
        }

        ArgumentNullException.ThrowIfNull(config);

        string configPath;
        try
        {
            configPath = GetConfigPath(storageRoot);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return UcliConfigSaveResult.Failure(ExecutionError.InvalidArgument(
                $"Storage root path is invalid: {storageRoot}"));
        }

        var documentBuildResult = compiler.CreateDocument(config, configPath);
        if (!documentBuildResult.IsSuccess)
        {
            return UcliConfigSaveResult.Failure(documentBuildResult.Diagnostics);
        }

        var json = JsonSerializer.Serialize(documentBuildResult.Document!, SerializerOptions);
        string? configDirectoryPath = null;
        try
        {
            configDirectoryPath = Path.GetDirectoryName(configPath);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return UcliConfigSaveResult.Failure(ExecutionError.InvalidArgument(
                $"Config path is invalid: {configPath}. {ex.Message}"));
        }

        if (string.IsNullOrWhiteSpace(configDirectoryPath))
        {
            return UcliConfigSaveResult.Failure(ExecutionError.InternalError(
                $"Config directory path could not be determined: {configPath}"));
        }

        try
        {
            Directory.CreateDirectory(configDirectoryPath);
            await File.WriteAllTextAsync(configPath, json + Environment.NewLine, cancellationToken).ConfigureAwait(false);
            return UcliConfigSaveResult.Success();
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return UcliConfigSaveResult.Failure(ExecutionError.InvalidArgument(
                $"Config path is invalid: {configPath}. {ex.Message}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return UcliConfigSaveResult.Failure(ExecutionError.InternalError(
                $"Failed to write config file: {configPath}. {ex.Message}"));
        }
    }

    /// <summary> Determines whether an exception should be treated as an internal I/O failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when it is an I/O failure; otherwise <see langword="false" />. </returns>
    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }

}
