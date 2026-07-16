namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents a daemon-only dirty loaded scene probe result. </summary>
internal sealed record SceneTreeLiteDirtySourceProbeResult
{
    private SceneTreeLiteDirtySourceProbeResult (
        SceneTreeLiteSourceSnapshot? snapshot,
        string? fallbackReason)
    {
        Snapshot = snapshot;
        FallbackReason = fallbackReason;
    }

    public SceneTreeLiteSourceSnapshot? Snapshot { get; }

    public string? FallbackReason { get; }

    /// <summary> Gets whether a dirty live source snapshot is available. </summary>
    public bool HasDirtySource => Snapshot is not null;

    /// <summary> Creates a result that contains a dirty live source snapshot. </summary>
    public static SceneTreeLiteDirtySourceProbeResult DirtySource (
        SceneTreeLiteSourceSnapshot snapshot,
        string fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReason);
        return new SceneTreeLiteDirtySourceProbeResult(snapshot, fallbackReason);
    }

    /// <summary> Creates a result that does not contain a dirty live source snapshot. </summary>
    public static SceneTreeLiteDirtySourceProbeResult NotAvailable (string? reason)
    {
        return new SceneTreeLiteDirtySourceProbeResult(null, reason);
    }
}
