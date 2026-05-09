using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start;

/// <summary> Reads Unity <c>Library/EditorInstance.json</c> markers from the resolved project root. </summary>
internal sealed class UnityEditorInstanceMarkerReader : IUnityEditorInstanceMarkerReader
{
    private const string LibraryDirectoryName = "Library";

    private const string MarkerFileName = "EditorInstance.json";

    private const string ProcessIdPropertyName = "process_id";

    private const string VersionPropertyName = "version";

    private const string AppPathPropertyName = "app_path";

    private const string AppContentsPathPropertyName = "app_contents_path";

    /// <inheritdoc />
    public async ValueTask<UnityEditorInstanceMarkerReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        string markerPath;
        try
        {
            markerPath = ResolveMarkerPath(unityProject.UnityProjectRoot);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InvalidArgument(
                $"Unity Editor instance marker path is invalid. {exception.Message}"));
        }

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNull(markerPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InvalidArgument(
                $"Unity Editor instance marker path is invalid: {markerPath}. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read Unity Editor instance marker: {markerPath}. {exception.Message}"));
        }

        if (json is null)
        {
            return UnityEditorInstanceMarkerReadResult.Success(null);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!TryReadProcessId(document.RootElement, out var processId))
            {
                return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InvalidArgument(
                    $"Unity Editor instance marker process_id is invalid: {markerPath}."));
            }

            var updatedAtUtc = File.GetLastWriteTimeUtc(markerPath);
            return UnityEditorInstanceMarkerReadResult.Success(new UnityEditorInstanceMarker(
                MarkerPath: markerPath,
                ProcessId: processId,
                UpdatedAtUtc: new DateTimeOffset(updatedAtUtc, TimeSpan.Zero),
                Version: ReadOptionalString(document.RootElement, VersionPropertyName),
                AppPath: ReadOptionalString(document.RootElement, AppPathPropertyName),
                AppContentsPath: ReadOptionalString(document.RootElement, AppContentsPathPropertyName)));
        }
        catch (JsonException exception)
        {
            return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InvalidArgument(
                $"Unity Editor instance marker JSON is invalid: {markerPath}. {exception.Message}"));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InvalidArgument(
                $"Unity Editor instance marker path is invalid: {markerPath}. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InternalError(
                $"Failed to inspect Unity Editor instance marker: {markerPath}. {exception.Message}"));
        }
    }

    private static string ResolveMarkerPath (string unityProjectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        return Path.Combine(
            unityProjectRoot,
            LibraryDirectoryName,
            MarkerFileName);
    }

    private static bool TryReadProcessId (
        JsonElement root,
        out int processId)
    {
        processId = default;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(ProcessIdPropertyName, out var processIdElement)
            || processIdElement.ValueKind != JsonValueKind.Number
            || !processIdElement.TryGetInt32(out var parsedProcessId)
            || parsedProcessId <= 0)
        {
            return false;
        }

        processId = parsedProcessId;
        return true;
    }

    private static string? ReadOptionalString (
        JsonElement root,
        string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return StringValueNormalizer.TrimToNull(property.GetString());
    }
}
