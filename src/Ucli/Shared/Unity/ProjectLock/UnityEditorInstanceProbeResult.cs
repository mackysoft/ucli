namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Represents one Unity EditorInstance marker probe result. </summary>
/// <param name="Status"> The marker probe status. </param>
/// <param name="EditorInstancePath"> The marker path that was inspected. </param>
/// <param name="ProcessId"> The marker process identifier when parsed. </param>
/// <param name="Message"> The diagnostic message when available. </param>
internal sealed record UnityEditorInstanceProbeResult (
    UnityEditorInstanceProbeStatus Status,
    string EditorInstancePath,
    int? ProcessId,
    string? Message)
{
    /// <summary> Creates a not-found result. </summary>
    /// <param name="editorInstancePath"> The marker path that was inspected. </param>
    /// <returns> The probe result. </returns>
    public static UnityEditorInstanceProbeResult NotFound (string editorInstancePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editorInstancePath);
        return new UnityEditorInstanceProbeResult(UnityEditorInstanceProbeStatus.NotFound, editorInstancePath, null, null);
    }

    /// <summary> Creates an active result. </summary>
    /// <param name="editorInstancePath"> The marker path that was inspected. </param>
    /// <param name="processId"> The live process identifier. </param>
    /// <returns> The probe result. </returns>
    public static UnityEditorInstanceProbeResult Active (
        string editorInstancePath,
        int processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editorInstancePath);
        return new UnityEditorInstanceProbeResult(UnityEditorInstanceProbeStatus.Active, editorInstancePath, processId, null);
    }

    /// <summary> Creates a stale result. </summary>
    /// <param name="editorInstancePath"> The marker path that was inspected. </param>
    /// <param name="processId"> The stale process identifier. </param>
    /// <returns> The probe result. </returns>
    public static UnityEditorInstanceProbeResult Stale (
        string editorInstancePath,
        int processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editorInstancePath);
        return new UnityEditorInstanceProbeResult(UnityEditorInstanceProbeStatus.Stale, editorInstancePath, processId, null);
    }

    /// <summary> Creates an ambiguous result. </summary>
    /// <param name="editorInstancePath"> The marker path that was inspected. </param>
    /// <param name="message"> The diagnostic message. </param>
    /// <returns> The probe result. </returns>
    public static UnityEditorInstanceProbeResult Ambiguous (
        string editorInstancePath,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editorInstancePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new UnityEditorInstanceProbeResult(UnityEditorInstanceProbeStatus.Ambiguous, editorInstancePath, null, message);
    }
}
