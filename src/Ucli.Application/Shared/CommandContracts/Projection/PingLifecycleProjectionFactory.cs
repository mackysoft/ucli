using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.CommandContracts.Projection;

/// <summary> Converts ping responses into shared command lifecycle projection values. </summary>
internal static class PingLifecycleProjectionFactory
{
    /// <summary> Creates normalized lifecycle projection values from one ping response. </summary>
    public static PingLifecycleProjection Create (IpcPingResponse pingResponse)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        var lifecycleState = IpcEditorLifecycleStateCodec.TryParse(pingResponse.LifecycleState, out var normalizedLifecycleState)
            ? normalizedLifecycleState
            : null;
        var blockingReason = lifecycleState is null || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal)
            ? null
            : IpcEditorBlockingReasonCodec.TryParse(pingResponse.BlockingReason, out var normalizedBlockingReason)
                ? normalizedBlockingReason
                : null;
        var canAcceptExecutionRequests = string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal)
            && pingResponse.CanAcceptExecutionRequests;

        return new PingLifecycleProjection(
            ServerVersion: StringValueNormalizer.TrimToNull(pingResponse.ServerVersion),
            UnityVersion: StringValueNormalizer.TrimToNull(pingResponse.UnityVersion),
            EditorMode: DaemonEditorModeCodec.TryParse(pingResponse.EditorMode, out var editorMode)
                ? DaemonEditorModeCodec.ToValue(editorMode)
                : null,
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileState: IpcCompileStateCodec.TryParse(pingResponse.CompileState, out var compileState)
                ? compileState
                : null,
            CompileGeneration: StringValueNormalizer.TrimToNull(pingResponse.CompileGeneration),
            DomainReloadGeneration: StringValueNormalizer.TrimToNull(pingResponse.DomainReloadGeneration),
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: pingResponse.ObservedAtUtc,
            ActionRequired: StringValueNormalizer.TrimToNull(pingResponse.ActionRequired),
            PrimaryDiagnostic: pingResponse.PrimaryDiagnostic,
            PlayMode: PlayModeSnapshotOutputFactory.Create(pingResponse.PlayMode));
    }
}
