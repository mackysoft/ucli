using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.EditorInstance;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.EditorInstance;

/// <summary> Reads Unity <c>Library/EditorInstance.json</c> markers from the resolved project root. </summary>
internal sealed class UnityEditorInstanceMarkerReader : IUnityEditorInstanceMarkerReader
{
    private const string ProcessIdPropertyName = "process_id";

    private const string AppPathPropertyName = "app_path";

    private const string AppContentsPathPropertyName = "app_contents_path";

    private const long MaxMarkerByteLength = 16 * 1024;

    /// <inheritdoc />
    public async ValueTask<UnityEditorInstanceMarkerReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var markerPath = UnityEditorInstanceMarkerPath.Resolve(unityProject.UnityProjectRoot);

        var markerSizeResult = ValidateMarkerSize(markerPath);
        if (markerSizeResult != null)
        {
            return UnityEditorInstanceMarkerReadResult.Failure(markerSizeResult);
        }

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(markerPath, cancellationToken).ConfigureAwait(false);
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
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 8,
            });
            if (!TryReadProcessId(document.RootElement, out var processId))
            {
                return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InvalidArgument(
                    $"Unity Editor instance marker process_id is invalid: {markerPath}."));
            }

            if (!TryReadOptionalAbsolutePath(document.RootElement, AppPathPropertyName, out var appPath))
            {
                return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InvalidArgument(
                    $"Unity Editor instance marker app_path is invalid: {markerPath}."));
            }

            if (!TryReadOptionalAbsolutePath(document.RootElement, AppContentsPathPropertyName, out var appContentsPath))
            {
                return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InvalidArgument(
                    $"Unity Editor instance marker app_contents_path is invalid: {markerPath}."));
            }

            var updatedAtUtc = File.GetLastWriteTimeUtc(markerPath.Value);
            return UnityEditorInstanceMarkerReadResult.Success(new UnityEditorInstanceMarker(
                MarkerPath: markerPath,
                ProcessId: processId,
                UpdatedAtUtc: new DateTimeOffset(updatedAtUtc, TimeSpan.Zero),
                AppPath: appPath,
                AppContentsPath: appContentsPath));
        }
        catch (JsonException exception)
        {
            return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InvalidArgument(
                $"Unity Editor instance marker JSON is invalid: {markerPath}. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return UnityEditorInstanceMarkerReadResult.Failure(ExecutionError.InternalError(
                $"Failed to inspect Unity Editor instance marker: {markerPath}. {exception.Message}"));
        }
    }

    private static ExecutionError? ValidateMarkerSize (AbsolutePath markerPath)
    {
        try
        {
            var fileInfo = new FileInfo(markerPath.Value);
            if (!fileInfo.Exists)
            {
                return null;
            }

            return fileInfo.Length <= MaxMarkerByteLength
                ? null
                : ExecutionError.InvalidArgument(
                    $"Unity Editor instance marker is too large: {markerPath}. MaxBytes={MaxMarkerByteLength} ActualBytes={fileInfo.Length}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ExecutionError.InternalError(
                $"Failed to inspect Unity Editor instance marker: {markerPath}. {exception.Message}");
        }
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

    private static bool TryReadOptionalAbsolutePath (
        JsonElement root,
        string propertyName,
        out AbsolutePath? path)
    {
        path = null;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return true;
        }

        var value = StringValueNormalizer.TrimToNull(property.GetString());
        return value is null
            || AbsolutePath.TryParse(value, out path, out _);
    }
}
