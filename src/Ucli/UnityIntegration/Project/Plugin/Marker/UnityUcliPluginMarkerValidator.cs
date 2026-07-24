using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;

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
        AbsolutePath markerPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var stream = File.OpenRead(markerPath.Value);
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
        AbsolutePath unityProjectRoot,
        AbsolutePath markerPath,
        [NotNullWhen(true)] out RootRelativePath? projectRelativeMarkerPath)
    {
        if (!ContainedPath.TryCreate(
                unityProjectRoot,
                markerPath,
                out var containedMarkerPath,
                out _))
        {
            projectRelativeMarkerPath = null;
            return false;
        }

        projectRelativeMarkerPath = containedMarkerPath.RelativePath;
        return true;
    }

    /// <summary> Tries to resolve an absolute marker path from one cached project-relative marker path. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="projectRelativeMarkerPath"> The cached project-relative marker path. </param>
    /// <param name="resolvedMarkerPath"> The resolved absolute marker path. </param>
    /// <returns> <see langword="true" /> when the cached path remained valid under the project root. </returns>
    public bool TryResolveCachedMarkerPath (
        AbsolutePath unityProjectRoot,
        string projectRelativeMarkerPath,
        [NotNullWhen(true)] out AbsolutePath? resolvedMarkerPath)
    {
        if (!RootRelativePath.TryParse(
                projectRelativeMarkerPath,
                out var relativeMarkerPath,
                out _))
        {
            resolvedMarkerPath = null;
            return false;
        }

        resolvedMarkerPath = ContainedPath.Create(
            unityProjectRoot,
            relativeMarkerPath).Target;
        return true;
    }

    /// <summary> Maps the marker JSON contract. </summary>
    private sealed record UnityUcliPluginMarkerJson (
        string? PluginId,
        int? ProtocolVersion);
}
