using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents a daemon-only dirty loaded scene probe result. </summary>
internal sealed record SceneTreeLiteDirtySourceProbeResult (
    IpcIndexSceneTreeLiteReadResponse? Response,
    string? FallbackReason)
{
    /// <summary> Gets whether a dirty live source snapshot is available. </summary>
    public bool HasDirtySource => Response is not null;

    /// <summary> Creates a result that contains a dirty live source snapshot. </summary>
    public static SceneTreeLiteDirtySourceProbeResult DirtySource (
        IpcIndexSceneTreeLiteReadResponse response,
        string fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReason);
        return new SceneTreeLiteDirtySourceProbeResult(response, fallbackReason);
    }

    /// <summary> Creates a result that does not contain a dirty live source snapshot. </summary>
    public static SceneTreeLiteDirtySourceProbeResult NotAvailable (string? reason)
    {
        return new SceneTreeLiteDirtySourceProbeResult(null, reason);
    }
}
