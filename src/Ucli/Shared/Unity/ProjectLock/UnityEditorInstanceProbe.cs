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
                "Library/EditorInstance.json",
                $"EditorInstance path could not be resolved. {exception.Message}");
        }

        string json;
        try
        {
            if (!File.Exists(editorInstancePath))
            {
                return UnityEditorInstanceProbeResult.NotFound(editorInstancePath);
            }

            json = await File.ReadAllTextAsync(editorInstancePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityEditorInstanceProbeResult.Ambiguous(
                editorInstancePath,
                $"EditorInstance path is invalid. {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return UnityEditorInstanceProbeResult.Ambiguous(
                editorInstancePath,
                $"EditorInstance marker could not be read. {exception.Message}");
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
                    editorInstancePath,
                    "EditorInstance marker does not contain a positive process_id.");
            }
        }
        catch (JsonException exception)
        {
            return UnityEditorInstanceProbeResult.Ambiguous(
                editorInstancePath,
                $"EditorInstance JSON is invalid. {exception.Message}");
        }

        return ProcessLivenessProbe.IsAlive(processId)
            ? UnityEditorInstanceProbeResult.Active(editorInstancePath, processId)
            : UnityEditorInstanceProbeResult.Stale(editorInstancePath, processId);
    }
}
