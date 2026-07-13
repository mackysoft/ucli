using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;

/// <summary> Represents endpoint-registered lifecycle values returned with daemon start success. </summary>
/// <param name="LifecycleState"> The normalized lifecycle state. </param>
internal sealed record DaemonStartLifecycleSnapshot (
    IpcEditorLifecycleState LifecycleState)
{
    /// <summary> Gets the blocking reason required by <see cref="LifecycleState" />. </summary>
    public IpcEditorBlockingReason? BlockingReason =>
        IpcEditorLifecycleSemantics.ResolveBlockingReason(LifecycleState);

    /// <summary> Gets whether <see cref="LifecycleState" /> permits normal execution requests. </summary>
    public bool CanAcceptExecutionRequests =>
        IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(LifecycleState);

    /// <summary> Creates a ready lifecycle snapshot for success paths that do not carry ping details. </summary>
    /// <returns> The ready lifecycle snapshot. </returns>
    public static DaemonStartLifecycleSnapshot Ready ()
    {
        return new DaemonStartLifecycleSnapshot(IpcEditorLifecycleState.Ready);
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
        if (!ContractLiteralCodec.TryParse<IpcEditorLifecycleState>(pingResponse.LifecycleState, out var lifecycleState))
        {
            error = ExecutionError.InternalError(
                $"Unity daemon startup probe returned unsupported lifecycleState '{pingResponse.LifecycleState}'.");
            return false;
        }

        if (!TryParseOptionalBlockingReason(pingResponse.BlockingReason, out var blockingReason))
        {
            error = ExecutionError.InternalError(
                $"Unity daemon startup probe returned unsupported blockingReason '{pingResponse.BlockingReason}'.");
            return false;
        }

        if (!IpcEditorLifecycleSemantics.IsConsistent(
                lifecycleState,
                blockingReason,
                pingResponse.CanAcceptExecutionRequests))
        {
            error = ExecutionError.InternalError(
                "Unity daemon startup probe returned an inconsistent lifecycle tuple. "
                + $"lifecycleState='{pingResponse.LifecycleState}', "
                + $"blockingReason='{pingResponse.BlockingReason ?? "<null>"}', "
                + $"canAcceptExecutionRequests={pingResponse.CanAcceptExecutionRequests}.");
            return false;
        }

        snapshot = new DaemonStartLifecycleSnapshot(lifecycleState);
        error = null;
        return true;
    }

    private static bool TryParseOptionalBlockingReason (
        string? value,
        out IpcEditorBlockingReason? blockingReason)
    {
        if (value is null)
        {
            blockingReason = null;
            return true;
        }

        if (ContractLiteralCodec.TryParse<IpcEditorBlockingReason>(value, out var parsedBlockingReason))
        {
            blockingReason = parsedBlockingReason;
            return true;
        }

        blockingReason = null;
        return false;
    }
}
