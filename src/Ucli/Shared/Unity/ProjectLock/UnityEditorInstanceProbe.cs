using System.Text.Json;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Reads Unity's project-local EditorInstance marker. </summary>
internal sealed class UnityEditorInstanceProbe : IUnityEditorInstanceProbe
{
    /// <inheritdoc />
    public async ValueTask<UnityEditorInstanceProbeResult> ProbeAsync (
        string unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);

        string editorInstancePath;
        try
        {
            editorInstancePath = Path.Combine(unityProjectRoot, "Library", "EditorInstance.json");
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityEditorInstanceProbeResult.Ambiguous(
                $"EditorInstance path could not be resolved. Path=Library/EditorInstance.json. {exception.Message}");
        }

        string json;
        try
        {
            if (!File.Exists(editorInstancePath))
            {
                return UnityEditorInstanceProbeResult.NotFound();
            }

            json = await File.ReadAllTextAsync(editorInstancePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityEditorInstanceProbeResult.Ambiguous(
                $"EditorInstance path is invalid. Path={editorInstancePath}. {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return UnityEditorInstanceProbeResult.Ambiguous(
                $"EditorInstance marker could not be read. Path={editorInstancePath}. {exception.Message}");
        }

        int processId;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("process_id", out var processIdElement)
                || !processIdElement.TryGetInt32(out processId)
                || processId <= 0)
            {
                return UnityEditorInstanceProbeResult.Ambiguous(
                    $"EditorInstance marker does not contain a positive process_id. Path={editorInstancePath}.");
            }
        }
        catch (JsonException exception)
        {
            return UnityEditorInstanceProbeResult.Ambiguous(
                $"EditorInstance JSON is invalid. Path={editorInstancePath}. {exception.Message}");
        }

        return ProcessLivenessProbe.IsAlive(processId)
            ? UnityEditorInstanceProbeResult.Active()
            : UnityEditorInstanceProbeResult.Stale();
    }
}
