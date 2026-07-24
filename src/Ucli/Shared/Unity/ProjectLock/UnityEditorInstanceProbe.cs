using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Reads Unity's project-local EditorInstance marker. </summary>
internal sealed class UnityEditorInstanceProbe : IUnityEditorInstanceProbe
{
    /// <inheritdoc />
    public async ValueTask<UnityEditorInstanceProbeResult> ProbeAsync (
        AbsolutePath unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProjectRoot);

        var editorInstancePath = ContainedPath.Create(
            unityProjectRoot,
            RootRelativePath.Parse("Library/EditorInstance.json")).Target;

        string json;
        try
        {
            if (!File.Exists(editorInstancePath.Value))
            {
                return UnityEditorInstanceProbeResult.NotFound();
            }

            json = await File.ReadAllTextAsync(editorInstancePath.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return UnityEditorInstanceProbeResult.Ambiguous(
                $"EditorInstance marker could not be read. Path={editorInstancePath.Value}. {exception.Message}");
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
                    $"EditorInstance marker does not contain a positive process_id. Path={editorInstancePath.Value}.");
            }
        }
        catch (JsonException exception)
        {
            return UnityEditorInstanceProbeResult.Ambiguous(
                $"EditorInstance JSON is invalid. Path={editorInstancePath.Value}. {exception.Message}");
        }

        return ProcessLivenessProbe.IsAlive(processId)
            ? UnityEditorInstanceProbeResult.Active()
            : UnityEditorInstanceProbeResult.Stale();
    }
}
