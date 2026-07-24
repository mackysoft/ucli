using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;

/// <summary> Persists one fingerprint-scoped runtime cache for the resolved uCLI Unity plugin marker. </summary>
internal sealed class UnityUcliPluginMarkerCacheStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly Func<AbsolutePath, CancellationToken, ValueTask<string?>> readAllTextOrNull;

    private readonly Func<AbsolutePath, string, CancellationToken, ValueTask> writeAllTextAtomically;

    private readonly Action<AbsolutePath> deleteIfExists;

    /// <summary> Initializes a new instance of the <see cref="UnityUcliPluginMarkerCacheStore" /> class. </summary>
    public UnityUcliPluginMarkerCacheStore ()
        : this(
            static (path, cancellationToken) => FileUtilities.ReadAllTextOrNullAsync(path, cancellationToken),
            static (path, contents, cancellationToken) => FileUtilities.WriteAllTextAtomicallyAsync(path, contents, cancellationToken),
            static path => FileUtilities.DeleteIfExists(path))
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UnityUcliPluginMarkerCacheStore" /> class for tests. </summary>
    /// <param name="readAllTextOrNull"> Delegate that reads cache JSON. </param>
    /// <param name="writeAllTextAtomically"> Delegate that writes cache JSON atomically. </param>
    /// <param name="deleteIfExists"> Delegate that deletes a cache file when present. </param>
    internal UnityUcliPluginMarkerCacheStore (
        Func<AbsolutePath, CancellationToken, ValueTask<string?>> readAllTextOrNull,
        Func<AbsolutePath, string, CancellationToken, ValueTask> writeAllTextAtomically,
        Action<AbsolutePath> deleteIfExists)
    {
        this.readAllTextOrNull = readAllTextOrNull ?? throw new ArgumentNullException(nameof(readAllTextOrNull));
        this.writeAllTextAtomically = writeAllTextAtomically ?? throw new ArgumentNullException(nameof(writeAllTextAtomically));
        this.deleteIfExists = deleteIfExists ?? throw new ArgumentNullException(nameof(deleteIfExists));
    }

    /// <summary> Reads one plugin-marker cache entry when present. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cache read result. </returns>
    public async ValueTask<UnityUcliPluginMarkerCacheReadResult> ReadOrNullAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cachePath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(
            storageRoot,
            projectFingerprint);

        string? json;
        try
        {
            json = await readAllTextOrNull(cachePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return UnityUcliPluginMarkerCacheReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read uCLI Unity plugin marker cache file: {cachePath}. {exception.Message}"));
        }

        if (json == null)
        {
            return UnityUcliPluginMarkerCacheReadResult.Success(null);
        }

        UnityUcliPluginMarkerCache cache;
        try
        {
            cache = JsonSerializer.Deserialize<UnityUcliPluginMarkerCache>(json, SerializerOptions)
                ?? throw new JsonException("uCLI Unity plugin marker cache JSON is null.");
            Validate(cache, cachePath);
        }
        catch (JsonException exception)
        {
            return UnityUcliPluginMarkerCacheReadResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker cache is invalid: {cachePath}. {exception.Message}"));
        }
        catch (ArgumentException exception)
        {
            return UnityUcliPluginMarkerCacheReadResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker cache is invalid: {cachePath}. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return UnityUcliPluginMarkerCacheReadResult.Failure(ExecutionError.InternalError(
                $"Failed to deserialize uCLI Unity plugin marker cache JSON: {cachePath}. {exception.Message}"));
        }

        return UnityUcliPluginMarkerCacheReadResult.Success(cache);
    }

    /// <summary> Writes one plugin-marker cache entry atomically. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cache"> The cache payload to persist. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cache operation result. </returns>
    public async ValueTask<UnityUcliPluginMarkerCacheStoreOperationResult> WriteAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        UnityUcliPluginMarkerCache cache,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(cache);

        var cachePath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(
            storageRoot,
            projectFingerprint);

        try
        {
            Validate(cache, cachePath);
            var json = JsonSerializer.Serialize(cache, SerializerOptions) + Environment.NewLine;
            var cacheDirectoryPath = UcliStoragePathResolver.ResolveProjectDirectory(
                storageRoot,
                projectFingerprint);
            FileSystemAccessBoundary.EnsureSecureDirectory(cacheDirectoryPath);
            await writeAllTextAtomically(cachePath, json, cancellationToken).ConfigureAwait(false);
            return UnityUcliPluginMarkerCacheStoreOperationResult.Success();
        }
        catch (ArgumentException exception)
        {
            return UnityUcliPluginMarkerCacheStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker cache is invalid: {cachePath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return UnityUcliPluginMarkerCacheStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write uCLI Unity plugin marker cache file: {cachePath}. {exception.Message}"));
        }
    }

    /// <summary> Deletes one plugin-marker cache entry when present. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The cache operation result. </returns>
    public UnityUcliPluginMarkerCacheStoreOperationResult DeleteIfExists (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        var cachePath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(
            storageRoot,
            projectFingerprint);

        try
        {
            deleteIfExists(cachePath);
            return UnityUcliPluginMarkerCacheStoreOperationResult.Success();
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return UnityUcliPluginMarkerCacheStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to delete uCLI Unity plugin marker cache file: {cachePath}. {exception.Message}"));
        }
    }

    private static void Validate (
        UnityUcliPluginMarkerCache cache,
        AbsolutePath cachePath)
    {
        ArgumentNullException.ThrowIfNull(cache);

        if (!RootRelativePath.TryParse(
                cache.ProjectRelativeMarkerPath,
                out _,
                out _))
        {
            throw new ArgumentException(
                "projectRelativeMarkerPath must be a valid root-relative path.",
                nameof(cache));
        }

        if (string.IsNullOrWhiteSpace(cache.PluginId))
        {
            throw new ArgumentException("pluginId must not be empty.", nameof(cache));
        }

        if (cache.ProtocolVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cache), cache.ProtocolVersion, $"plugin marker cache protocolVersion must be greater than zero. {cachePath}");
        }
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }
}
