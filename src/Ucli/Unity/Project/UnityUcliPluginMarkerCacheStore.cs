using System.Text.Json;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.UnityProject;

/// <summary> Persists one fingerprint-scoped runtime cache for the resolved uCLI Unity plugin marker. </summary>
internal sealed class UnityUcliPluginMarkerCacheStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly Func<string, CancellationToken, ValueTask<string?>> readAllTextOrNull;

    private readonly Func<string, string, CancellationToken, ValueTask> writeAllTextAtomically;

    private readonly Action<string> deleteIfExists;

    /// <summary> Initializes a new instance of the <see cref="UnityUcliPluginMarkerCacheStore" /> class. </summary>
    public UnityUcliPluginMarkerCacheStore ()
        : this(
            static (path, cancellationToken) => FileUtilities.ReadAllTextOrNull(path, cancellationToken),
            static (path, contents, cancellationToken) => FileUtilities.WriteAllTextAtomically(path, contents, cancellationToken),
            static path => FileUtilities.DeleteIfExists(path))
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UnityUcliPluginMarkerCacheStore" /> class for tests. </summary>
    /// <param name="readAllTextOrNull"> Delegate that reads cache JSON. </param>
    /// <param name="writeAllTextAtomically"> Delegate that writes cache JSON atomically. </param>
    /// <param name="deleteIfExists"> Delegate that deletes a cache file when present. </param>
    internal UnityUcliPluginMarkerCacheStore (
        Func<string, CancellationToken, ValueTask<string?>> readAllTextOrNull,
        Func<string, string, CancellationToken, ValueTask> writeAllTextAtomically,
        Action<string> deleteIfExists)
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
    public async ValueTask<UnityUcliPluginMarkerCacheReadResult> ReadOrNull (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string cachePath;
        try
        {
            cachePath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityUcliPluginMarkerCacheReadResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker cache path is invalid. {exception.Message}"));
        }

        string? json;
        try
        {
            json = await readAllTextOrNull(cachePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityUcliPluginMarkerCacheReadResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker cache path is invalid: {cachePath}. {exception.Message}"));
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
    public async ValueTask<UnityUcliPluginMarkerCacheStoreOperationResult> Write (
        string storageRoot,
        string projectFingerprint,
        UnityUcliPluginMarkerCache cache,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(cache);

        string cachePath;
        try
        {
            cachePath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityUcliPluginMarkerCacheStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker cache path is invalid. {exception.Message}"));
        }

        try
        {
            Validate(cache, cachePath);
            var json = JsonSerializer.Serialize(cache, SerializerOptions) + Environment.NewLine;
            await writeAllTextAtomically(cachePath, json, cancellationToken).ConfigureAwait(false);
            return UnityUcliPluginMarkerCacheStoreOperationResult.Success();
        }
        catch (ArgumentException exception)
        {
            return UnityUcliPluginMarkerCacheStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker cache is invalid: {cachePath}. {exception.Message}"));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityUcliPluginMarkerCacheStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker cache path is invalid: {cachePath}. {exception.Message}"));
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
        string storageRoot,
        string projectFingerprint)
    {
        string cachePath;
        try
        {
            cachePath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityUcliPluginMarkerCacheStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker cache path is invalid. {exception.Message}"));
        }

        try
        {
            deleteIfExists(cachePath);
            return UnityUcliPluginMarkerCacheStoreOperationResult.Success();
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityUcliPluginMarkerCacheStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker cache path is invalid: {cachePath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return UnityUcliPluginMarkerCacheStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to delete uCLI Unity plugin marker cache file: {cachePath}. {exception.Message}"));
        }
    }

    private static void Validate (
        UnityUcliPluginMarkerCache cache,
        string cachePath)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);

        if (string.IsNullOrWhiteSpace(cache.ProjectRelativeMarkerPath))
        {
            throw new ArgumentException("projectRelativeMarkerPath must not be empty.", nameof(cache));
        }

        if (Path.IsPathRooted(PathStringNormalizer.ToPlatformSeparated(cache.ProjectRelativeMarkerPath)))
        {
            throw new ArgumentException("projectRelativeMarkerPath must be relative.", nameof(cache));
        }

        var normalizedProjectRelativeMarkerPath = PathStringNormalizer.TrimTrailingDirectorySeparators(
            PathStringNormalizer.ToPlatformSeparated(cache.ProjectRelativeMarkerPath));
        var segments = normalizedProjectRelativeMarkerPath.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (string.Equals(segment, ".", StringComparison.Ordinal)
                || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                throw new ArgumentException("projectRelativeMarkerPath must not contain traversal tokens.", nameof(cache));
            }
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