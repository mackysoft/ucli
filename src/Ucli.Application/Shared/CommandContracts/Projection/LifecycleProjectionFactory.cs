using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.CommandContracts.Projection;

/// <summary> Converts lifecycle-bearing IPC responses into shared command lifecycle projection values. </summary>
internal static class LifecycleProjectionFactory
{
    /// <summary> Creates normalized lifecycle projection values from one ping response. </summary>
    public static LifecycleProjection Create (IpcPingResponse pingResponse)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        return Create(
            pingResponse.ServerVersion,
            pingResponse.UnityVersion,
            pingResponse.EditorMode,
            pingResponse.LifecycleState,
            pingResponse.BlockingReason,
            pingResponse.CompileState,
            pingResponse.CompileGeneration,
            pingResponse.DomainReloadGeneration,
            pingResponse.CanAcceptExecutionRequests,
            pingResponse.ObservedAtUtc,
            pingResponse.ActionRequired,
            pingResponse.PrimaryDiagnostic,
            pingResponse.PlayMode);
    }

    /// <summary> Creates normalized lifecycle projection values from one Play Mode lifecycle snapshot. </summary>
    public static LifecycleProjection Create (IpcPlayLifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return Create(
            snapshot.ServerVersion,
            snapshot.UnityVersion,
            snapshot.EditorMode,
            snapshot.LifecycleState,
            snapshot.BlockingReason,
            snapshot.CompileState,
            snapshot.CompileGeneration,
            snapshot.DomainReloadGeneration,
            snapshot.CanAcceptExecutionRequests,
            snapshot.ObservedAtUtc,
            snapshot.ActionRequired,
            snapshot.PrimaryDiagnostic,
            snapshot.PlayMode);
    }

    private static LifecycleProjection Create (
        string? serverVersion,
        string? unityVersion,
        string? editorModeValue,
        string? lifecycleStateValue,
        string? blockingReasonValue,
        string? compileStateValue,
        string? compileGeneration,
        string? domainReloadGeneration,
        bool canAcceptExecutionRequestsValue,
        DateTimeOffset? observedAtUtc,
        string? actionRequired,
        IpcPrimaryDiagnostic? primaryDiagnostic,
        IpcPlayModeSnapshot? playModeSnapshot)
    {
        IpcEditorLifecycleState? lifecycleState = null;
        IpcEditorBlockingReason? blockingReason = null;
        var canAcceptExecutionRequests = false;
        if (ContractLiteralCodec.TryParse<IpcEditorLifecycleState>(lifecycleStateValue, out var parsedLifecycleState)
            && TryParseOptionalBlockingReason(blockingReasonValue, out var parsedBlockingReason)
            && IpcEditorLifecycleSemantics.IsConsistent(
                parsedLifecycleState,
                parsedBlockingReason,
                canAcceptExecutionRequestsValue))
        {
            lifecycleState = parsedLifecycleState;
            blockingReason = parsedBlockingReason;
            canAcceptExecutionRequests = canAcceptExecutionRequestsValue;
        }

        return new LifecycleProjection(
            ServerVersion: StringValueNormalizer.TrimToNull(serverVersion),
            UnityVersion: StringValueNormalizer.TrimToNull(unityVersion),
            EditorMode: ContractLiteralInputParser.TryParseTrimmed<DaemonEditorMode>(editorModeValue, out var editorMode)
                ? ContractLiteralCodec.ToValue(editorMode)
                : null,
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileState: ContractLiteralCodec.TryParse<IpcCompileState>(compileStateValue, out var compileState)
                ? compileState
                : (IpcCompileState?)null,
            CompileGeneration: StringValueNormalizer.TrimToNull(compileGeneration),
            DomainReloadGeneration: StringValueNormalizer.TrimToNull(domainReloadGeneration),
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: observedAtUtc,
            ActionRequired: StringValueNormalizer.TrimToNull(actionRequired),
            PrimaryDiagnostic: primaryDiagnostic,
            PlayMode: PlayModeSnapshotOutputFactory.Create(playModeSnapshot));
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
