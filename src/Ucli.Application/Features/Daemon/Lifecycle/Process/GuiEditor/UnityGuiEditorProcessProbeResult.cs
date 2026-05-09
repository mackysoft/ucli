namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.GuiEditor;

/// <summary> Represents verification details for one Unity GUI Editor process marker candidate. </summary>
/// <param name="Status"> The probe status. </param>
/// <param name="ProcessStartedAtUtc"> The verified process start timestamp when the candidate matches. </param>
internal sealed record UnityGuiEditorProcessProbeResult (
    UnityGuiEditorProcessProbeStatus Status,
    DateTimeOffset? ProcessStartedAtUtc = null)
{
    /// <summary> Gets a value indicating whether the candidate is a verified GUI Editor process. </summary>
    public bool IsMatchingGuiEditor => Status == UnityGuiEditorProcessProbeStatus.MatchingGuiEditor;

    /// <summary> Creates one successful GUI Editor process verification result. </summary>
    public static UnityGuiEditorProcessProbeResult Matching (DateTimeOffset processStartedAtUtc)
    {
        return new UnityGuiEditorProcessProbeResult(UnityGuiEditorProcessProbeStatus.MatchingGuiEditor, processStartedAtUtc);
    }

    /// <summary> Creates one non-matching process verification result. </summary>
    public static UnityGuiEditorProcessProbeResult NotMatching (UnityGuiEditorProcessProbeStatus status)
    {
        if (status == UnityGuiEditorProcessProbeStatus.MatchingGuiEditor)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Use Matching for successful process verification.");
        }

        return new UnityGuiEditorProcessProbeResult(status);
    }
}
