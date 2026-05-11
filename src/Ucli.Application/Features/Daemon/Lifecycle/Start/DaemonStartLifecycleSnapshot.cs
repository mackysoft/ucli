using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Represents endpoint-registered lifecycle values returned with daemon start success. </summary>
/// <param name="LifecycleState"> The normalized lifecycle-state literal. </param>
/// <param name="BlockingReason"> The normalized blocking-reason literal when the lifecycle is blocked. </param>
/// <param name="CanAcceptExecutionRequests"> Whether ordinary execution requests can currently be accepted. </param>
internal sealed record DaemonStartLifecycleSnapshot (
    string LifecycleState,
    string? BlockingReason,
    bool CanAcceptExecutionRequests)
{
    /// <summary> Creates a ready lifecycle snapshot for legacy success paths that do not carry ping details. </summary>
    /// <returns> The ready lifecycle snapshot. </returns>
    public static DaemonStartLifecycleSnapshot Ready ()
    {
        return new DaemonStartLifecycleSnapshot(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            true);
    }

    /// <summary> Tries to create a normalized lifecycle snapshot from a ping payload. </summary>
    /// <param name="pingResponse"> The daemon ping response. </param>
    /// <param name="snapshot"> The lifecycle snapshot when parsing succeeds. </param>
    /// <param name="error"> The structured parsing error when parsing fails. </param>
    /// <returns> <see langword="true" /> when the snapshot was created; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        IpcPingResponse pingResponse,
        out DaemonStartLifecycleSnapshot? snapshot,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        snapshot = null;
        if (!IpcEditorLifecycleStateCodec.TryParse(pingResponse.LifecycleState, out var lifecycleState))
        {
            error = ExecutionError.InternalError(
                $"Unity daemon startup probe returned unsupported lifecycleState '{pingResponse.LifecycleState}'.");
            return false;
        }

        var blockingReason = string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal)
            ? null
            : IpcEditorBlockingReasonCodec.TryParse(pingResponse.BlockingReason, out var normalizedBlockingReason)
                ? normalizedBlockingReason
                : null;
        snapshot = new DaemonStartLifecycleSnapshot(
            lifecycleState!,
            blockingReason,
            string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal)
                && pingResponse.CanAcceptExecutionRequests);
        error = null;
        return true;
    }
}
