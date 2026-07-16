namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.GuiEditor;

/// <summary> Represents verification details for one Unity GUI Editor process marker candidate. </summary>
internal sealed record UnityGuiEditorProcessProbeResult
{
    private UnityGuiEditorProcessProbeResult (
        UnityGuiEditorProcessProbeStatus status,
        DateTimeOffset? processStartedAtUtc)
    {
        var isMatching = status == UnityGuiEditorProcessProbeStatus.MatchingGuiEditor;
        if (!isMatching && !IsDefinedNonMatchingStatus(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "GUI Editor process probe status must be defined.");
        }

        if (isMatching)
        {
            if (processStartedAtUtc is not DateTimeOffset matchingProcessStartedAtUtc)
            {
                throw new ArgumentNullException(nameof(processStartedAtUtc));
            }

            ProcessStartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
                matchingProcessStartedAtUtc,
                nameof(processStartedAtUtc));
        }
        else
        {
            if (processStartedAtUtc is not null)
            {
                throw new ArgumentException(
                    "A non-matching GUI Editor process result must not contain a process start timestamp.",
                    nameof(processStartedAtUtc));
            }

            ProcessStartedAtUtc = null;
        }

        Status = status;
    }

    /// <summary> Gets the process verification status. </summary>
    public UnityGuiEditorProcessProbeStatus Status { get; }

    /// <summary> Gets the verified process start timestamp for a matching GUI Editor; otherwise <see langword="null" />. </summary>
    public DateTimeOffset? ProcessStartedAtUtc { get; }

    /// <summary> Gets a value indicating whether the candidate is a verified GUI Editor process. </summary>
    public bool IsMatchingGuiEditor => Status == UnityGuiEditorProcessProbeStatus.MatchingGuiEditor;

    /// <summary> Creates one successful GUI Editor process verification result. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="processStartedAtUtc" /> is not a non-default UTC timestamp. </exception>
    public static UnityGuiEditorProcessProbeResult Matching (DateTimeOffset processStartedAtUtc)
    {
        return new UnityGuiEditorProcessProbeResult(
            UnityGuiEditorProcessProbeStatus.MatchingGuiEditor,
            processStartedAtUtc);
    }

    /// <summary> Creates one non-matching process verification result. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="status" /> does not identify a defined non-matching outcome. </exception>
    public static UnityGuiEditorProcessProbeResult NotMatching (UnityGuiEditorProcessProbeStatus status)
    {
        if (!IsDefinedNonMatchingStatus(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "GUI Editor process probe status must identify a non-matching outcome.");
        }

        return new UnityGuiEditorProcessProbeResult(status, processStartedAtUtc: null);
    }

    private static bool IsDefinedNonMatchingStatus (UnityGuiEditorProcessProbeStatus status)
    {
        return status is
            UnityGuiEditorProcessProbeStatus.NotRunning
            or UnityGuiEditorProcessProbeStatus.DifferentUser
            or UnityGuiEditorProcessProbeStatus.Batchmode
            or UnityGuiEditorProcessProbeStatus.NotUnityEditor
            or UnityGuiEditorProcessProbeStatus.StaleMarker
            or UnityGuiEditorProcessProbeStatus.Uncertain;
    }
}
