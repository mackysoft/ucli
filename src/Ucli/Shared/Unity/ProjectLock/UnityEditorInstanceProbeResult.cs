namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Represents one Unity EditorInstance marker probe result. </summary>
/// <param name="Status"> The marker probe status. </param>
/// <param name="Message"> The diagnostic message when available. </param>
internal sealed record UnityEditorInstanceProbeResult (
    UnityEditorInstanceProbeStatus Status,
    string? Message)
{
    /// <summary> Creates a not-found result. </summary>
    /// <returns> The probe result. </returns>
    public static UnityEditorInstanceProbeResult NotFound ()
    {
        return new UnityEditorInstanceProbeResult(UnityEditorInstanceProbeStatus.NotFound, null);
    }

    /// <summary> Creates an active result. </summary>
    /// <returns> The probe result. </returns>
    public static UnityEditorInstanceProbeResult Active ()
    {
        return new UnityEditorInstanceProbeResult(UnityEditorInstanceProbeStatus.Active, null);
    }

    /// <summary> Creates a stale result. </summary>
    /// <returns> The probe result. </returns>
    public static UnityEditorInstanceProbeResult Stale ()
    {
        return new UnityEditorInstanceProbeResult(UnityEditorInstanceProbeStatus.Stale, null);
    }

    /// <summary> Creates an ambiguous result. </summary>
    /// <param name="message"> The diagnostic message. </param>
    /// <returns> The probe result. </returns>
    public static UnityEditorInstanceProbeResult Ambiguous (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new UnityEditorInstanceProbeResult(UnityEditorInstanceProbeStatus.Ambiguous, message);
    }
}
