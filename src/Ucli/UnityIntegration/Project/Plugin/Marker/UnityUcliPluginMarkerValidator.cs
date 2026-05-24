using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Marker;

/// <summary> Validates uCLI Unity plugin marker files and converts marker paths to project-relative cache values. </summary>
internal sealed class UnityUcliPluginMarkerValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary> Reads and validates one marker file. </summary>
    /// <param name="markerPath"> The marker file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The structured error on failure; otherwise <see langword="null" />. </returns>
    public async ValueTask<ExecutionError?> ValidateMarkerAsync (
        string markerPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(markerPath);

        try
        {
            await using var stream = File.OpenRead(markerPath);
            var marker = await JsonSerializer.DeserializeAsync<UnityUcliPluginMarkerJson>(
                    stream,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            if (marker == null)
            {
                return ExecutionError.InvalidArgument(
                    $"uCLI Unity plugin marker is invalid. Path='{markerPath}'. Reason=Marker JSON is empty.");
            }

            if (!string.Equals(marker.PluginId, UnityUcliPluginMarkerContract.ExpectedPluginId, StringComparison.Ordinal))
            {
                return ExecutionError.InvalidArgument(
                    $"uCLI Unity plugin marker is invalid. Path='{markerPath}'. Reason=pluginId must be '{UnityUcliPluginMarkerContract.ExpectedPluginId}'.");
            }

            if (marker.ProtocolVersion != UnityUcliPluginMarkerContract.ExpectedProtocolVersion)
            {
                return ExecutionError.InvalidArgument(
                    $"uCLI Unity plugin marker is invalid. Path='{markerPath}'. Reason=protocolVersion must be '{UnityUcliPluginMarkerContract.ExpectedProtocolVersion}'.");
            }

            return null;
        }
        catch (JsonException exception)
        {
            return ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker is invalid. Path='{markerPath}'. Reason=Marker JSON could not be parsed. {exception.Message}");
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker is invalid. Path='{markerPath}'. Reason=Marker path is invalid. {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ExecutionError.InvalidArgument(
                $"uCLI Unity plugin marker is invalid. Path='{markerPath}'. Reason=Marker file could not be read. {exception.Message}");
        }
    }

    /// <summary> Tries to create a project-relative marker path. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="markerPath"> The absolute marker path. </param>
    /// <param name="projectRelativeMarkerPath"> The resolved project-relative marker path. </param>
    /// <returns> <see langword="true" /> when conversion succeeded. </returns>
    public bool TryCreateProjectRelativeMarkerPath (
        string unityProjectRoot,
        string markerPath,
        out string? projectRelativeMarkerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerPath);

        try
        {
            var normalizedUnityProjectRoot = Path.GetFullPath(unityProjectRoot);
            var normalizedMarkerPath = Path.GetFullPath(markerPath);
            if (!IsUnderProjectRoot(normalizedUnityProjectRoot, normalizedMarkerPath))
            {
                projectRelativeMarkerPath = null;
                return false;
            }

            projectRelativeMarkerPath = Path.GetRelativePath(
                normalizedUnityProjectRoot,
                normalizedMarkerPath);
            projectRelativeMarkerPath = PathStringNormalizer.TrimTrailingDirectorySeparators(projectRelativeMarkerPath);
            projectRelativeMarkerPath = PathStringNormalizer.ToSlashSeparated(projectRelativeMarkerPath);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                          || PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            projectRelativeMarkerPath = null;
            return false;
        }
    }

    /// <summary> Tries to resolve an absolute marker path from one cached project-relative marker path. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="projectRelativeMarkerPath"> The cached project-relative marker path. </param>
    /// <param name="resolvedMarkerPath"> The resolved absolute marker path. </param>
    /// <returns> <see langword="true" /> when the cached path remained valid under the project root. </returns>
    public bool TryResolveCachedMarkerPath (
        string unityProjectRoot,
        string projectRelativeMarkerPath,
        out string? resolvedMarkerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRelativeMarkerPath);

        try
        {
            var normalizedUnityProjectRoot = Path.GetFullPath(unityProjectRoot);
            var platformRelativeMarkerPath = PathStringNormalizer.ToPlatformSeparated(projectRelativeMarkerPath);
            resolvedMarkerPath = Path.GetFullPath(Path.Combine(
                normalizedUnityProjectRoot,
                platformRelativeMarkerPath));
            if (!IsUnderProjectRoot(normalizedUnityProjectRoot, resolvedMarkerPath))
            {
                resolvedMarkerPath = null;
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                          || PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            resolvedMarkerPath = null;
            return false;
        }
    }

    private static bool IsUnderProjectRoot (
        string unityProjectRoot,
        string markerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerPath);

        var normalizedProjectRoot = PathStringNormalizer.NormalizeCaseForCurrentPlatform(
            PathStringNormalizer.TrimTrailingDirectorySeparators(Path.GetFullPath(unityProjectRoot)));
        var normalizedMarkerPath = PathStringNormalizer.NormalizeCaseForCurrentPlatform(Path.GetFullPath(markerPath));

        if (!normalizedMarkerPath.StartsWith(normalizedProjectRoot, StringComparison.Ordinal))
        {
            return false;
        }

        if (normalizedMarkerPath.Length == normalizedProjectRoot.Length)
        {
            return true;
        }

        var nextCharacter = normalizedMarkerPath[normalizedProjectRoot.Length];
        return nextCharacter == Path.DirectorySeparatorChar
            || nextCharacter == Path.AltDirectorySeparatorChar;
    }

    /// <summary> Maps the marker JSON contract. </summary>
    private sealed record UnityUcliPluginMarkerJson (
        string? PluginId,
        int? ProtocolVersion);
}
