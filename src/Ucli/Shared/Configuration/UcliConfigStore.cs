using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;
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
    public AbsolutePath GetConfigPath (AbsolutePath storageRoot)
    {
        return UcliStoragePathResolver.ResolveConfigPath(storageRoot);
    }

    /// <summary> Loads configuration values for a storage root. </summary>
    /// <param name="storageRoot"> The guarded storage-root path from command context. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the config-load result. When <c>.ucli/config.json</c> does not exist, default config values are returned with <see cref="ConfigSource.Default" />. </returns>
    public async ValueTask<UcliConfigLoadResult> LoadAsync (
        AbsolutePath storageRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var configPath = GetConfigPath(storageRoot);

        if (!File.Exists(configPath.Value))
        {
            return UcliConfigLoadResult.Success(UcliConfig.CreateDefault(), ConfigSource.Default);
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(configPath.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return UcliConfigLoadResult.Failure(ExecutionError.InternalError(
                $"Failed to read config file: {configPath}. {ex.Message}"));
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(json);
            var compileResult = compiler.Compile(jsonDocument.RootElement, configPath.Value);
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
                    sourcePath: configPath.Value,
                    $"Config JSON is invalid: {ex.Message}"),
            ]);
        }
    }

    /// <summary> Saves configuration values to <c>.ucli/config.json</c>. </summary>
    /// <param name="storageRoot"> The guarded storage-root path from command context. </param>
    /// <param name="config"> The config values to persist. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the config-save result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public async ValueTask<UcliConfigSaveResult> SaveAsync (
        AbsolutePath storageRoot,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(config);

        var configPath = GetConfigPath(storageRoot);

        var documentBuildResult = compiler.CreateDocument(config, configPath.Value);
        if (!documentBuildResult.IsSuccess)
        {
            return UcliConfigSaveResult.Failure(documentBuildResult.Diagnostics);
        }

        var json = JsonSerializer.Serialize(documentBuildResult.Document!, SerializerOptions);
        var configDirectoryPath = UcliStoragePathResolver.ResolveUcliDirectoryPath(storageRoot);

        try
        {
            Directory.CreateDirectory(configDirectoryPath.Value);
            await File.WriteAllTextAsync(
                    configPath.Value,
                    json + Environment.NewLine,
                    cancellationToken)
                .ConfigureAwait(false);
            return UcliConfigSaveResult.Success();
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
