using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Represents verification details for one Unity GUI Editor process marker candidate. </summary>
/// <param name="Status"> The probe status. </param>
/// <param name="ObservedStartTimeUtc"> The observed process start time when available. </param>
/// <param name="Error"> The structured verification error when one is available. </param>
internal sealed record UnityGuiEditorProcessProbeResult (
    UnityGuiEditorProcessProbeStatus Status,
    DateTimeOffset? ObservedStartTimeUtc,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the candidate is a verified GUI Editor process. </summary>
    public bool IsMatchingGuiEditor => Status == UnityGuiEditorProcessProbeStatus.MatchingGuiEditor;

    /// <summary> Creates one successful GUI Editor process verification result. </summary>
    public static UnityGuiEditorProcessProbeResult Matching (DateTimeOffset processStartTimeUtc)
    {
        return new UnityGuiEditorProcessProbeResult(
            UnityGuiEditorProcessProbeStatus.MatchingGuiEditor,
            processStartTimeUtc,
            null);
    }

    /// <summary> Creates one non-matching process verification result. </summary>
    public static UnityGuiEditorProcessProbeResult NotMatching (
        UnityGuiEditorProcessProbeStatus status,
        DateTimeOffset? processStartTimeUtc = null,
        ExecutionError? error = null)
    {
        if (status == UnityGuiEditorProcessProbeStatus.MatchingGuiEditor)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Use Matching for successful process verification.");
        }

        return new UnityGuiEditorProcessProbeResult(status, processStartTimeUtc, error);
    }
}
