using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Converts ping payloads into ready lifecycle evidence. </summary>
internal static class ReadyLifecycleOutputFactory
{
    /// <summary> Creates lifecycle evidence from one ping response. </summary>
    public static ReadyLifecycleOutput Create (UnityEditorObservation pingResponse)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        var state = pingResponse.State;

        return new ReadyLifecycleOutput(
            ServerVersion: StringValueNormalizer.TrimToNull(pingResponse.ServerVersion),
            UnityVersion: StringValueNormalizer.TrimToNull(pingResponse.UnityVersion),
            EditorMode: state.EditorMode,
            LifecycleState: state.LifecycleState,
            BlockingReason: UnityEditorLifecycleSemantics.ResolveBlockingReason(state.LifecycleState),
            CompileState: state.CompileState,
            Generations: state.Generations,
            CanAcceptExecutionRequests: UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(state.LifecycleState),
            ObservedAtUtc: pingResponse.ObservedAtUtc,
            ActionRequired: pingResponse.ActionRequired,
            PrimaryDiagnostic: ToOutput(pingResponse.PrimaryDiagnostic),
            PlayMode: state.PlayMode);
    }

    private static ReadyPrimaryDiagnosticOutput? ToOutput (UnityEditorPrimaryDiagnostic? diagnostic)
    {
        if (diagnostic?.Kind is not { } kind)
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
