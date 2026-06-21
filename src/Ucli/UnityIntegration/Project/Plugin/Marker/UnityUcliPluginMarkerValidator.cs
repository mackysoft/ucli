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
            var markerPathResult = RepositoryPathNormalizer.TryNormalize(unityProjectRoot, markerPath);
            if (!markerPathResult.IsSuccess)
            {
                projectRelativeMarkerPath = null;
                return false;
            }

            projectRelativeMarkerPath = markerPathResult.RepositoryRelativeSlashPath;
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
            if (!RelativePathContract.TryNormalize(projectRelativeMarkerPath, out var normalizedRelativeMarkerPath))
            {
                resolvedMarkerPath = null;
                return false;
            }

            var markerPathResult = RepositoryPathNormalizer.TryNormalize(unityProjectRoot, normalizedRelativeMarkerPath);
            if (!markerPathResult.IsSuccess)
            {
                resolvedMarkerPath = null;
                return false;
            }

            resolvedMarkerPath = markerPathResult.FullPath;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                          || PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            resolvedMarkerPath = null;
            return false;
        }
    }

    /// <summary> Maps the marker JSON contract. </summary>
    private sealed record UnityUcliPluginMarkerJson (
        string? PluginId,
        int? ProtocolVersion);
}
