using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Converts ping payloads into ready lifecycle evidence. </summary>
internal static class ReadyLifecycleOutputFactory
{
    /// <summary> Creates lifecycle evidence from one ping response. </summary>
    public static ReadyLifecycleOutput Create (IpcPingResponse pingResponse)
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

        return new ReadyLifecycleOutput(
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
            PrimaryDiagnostic: ToOutput(pingResponse.PrimaryDiagnostic));
    }

    private static ReadyPrimaryDiagnosticOutput? ToOutput (IpcPrimaryDiagnostic? diagnostic)
    {
        if (diagnostic is null || !StringValueNormalizer.TryTrimToNonEmpty(diagnostic.Kind, out var kind))
        {
            return null;
        }

        return new ReadyPrimaryDiagnosticOutput(
            Kind: kind,
            Code: StringValueNormalizer.TrimToNull(diagnostic.Code),
            File: StringValueNormalizer.TrimToNull(diagnostic.File),
            Line: diagnostic.Line,
            Column: diagnostic.Column,
            Message: StringValueNormalizer.TrimToNull(diagnostic.Message));
    }
}
