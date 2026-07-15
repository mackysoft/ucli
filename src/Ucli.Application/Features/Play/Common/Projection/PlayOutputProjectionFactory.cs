using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Play.Common.Projection;

/// <summary> Creates public Play Mode command projection values from Unity Editor observations. </summary>
internal static class PlayOutputProjectionFactory
{
    /// <summary> Creates one public lifecycle output from a Unity Editor observation. </summary>
    /// <param name="observation"> The Unity Editor observation. </param>
    /// <returns> The public lifecycle snapshot output. </returns>
    public static PlayLifecycleSnapshotOutput CreateSnapshotOutput (IpcUnityEditorObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var state = observation.State;
        return new PlayLifecycleSnapshotOutput(
            ServerVersion: StringValueNormalizer.TrimToNull(observation.ServerVersion),
            EditorMode: state.EditorMode,
            UnityVersion: StringValueNormalizer.TrimToNull(observation.UnityVersion),
            ProjectFingerprint: observation.ProjectFingerprint,
            LifecycleState: state.LifecycleState,
            BlockingReason: IpcEditorLifecycleSemantics.ResolveBlockingReason(state.LifecycleState),
            CompileState: state.CompileState,
            Generations: state.Generations,
            CanAcceptExecutionRequests: IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(state.LifecycleState),
            ObservedAtUtc: observation.ObservedAtUtc,
            ActionRequired: observation.ActionRequired,
            PrimaryDiagnostic: CreatePrimaryDiagnosticOutput(observation.PrimaryDiagnostic),
            PlayMode: state.PlayMode);
    }

    /// <summary> Creates one public transition output from a validated IPC transition result. </summary>
    /// <param name="transition"> The validated IPC transition result. </param>
    /// <returns> The projected transition output. </returns>
    public static PlayTransitionOutput CreateTransitionOutput (IpcPlayTransitionResult transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        return new PlayTransitionOutput(
            Transition: transition.Transition,
            Result: transition.Result,
            Before: CreateSnapshotOutput(transition.Before),
            After: transition.After is null ? null : CreateSnapshotOutput(transition.After),
            Observed: transition.Observed is null ? null : CreateSnapshotOutput(transition.Observed),
            ApplicationState: transition.ApplicationState);
    }

    /// <summary> Creates one public primary diagnostic projection. </summary>
    /// <param name="diagnostic"> The IPC diagnostic contract. </param>
    /// <returns> The diagnostic output, or <see langword="null" /> when absent or invalid. </returns>
    public static DaemonPrimaryDiagnosticOutput? CreatePrimaryDiagnosticOutput (IpcPrimaryDiagnostic? diagnostic)
    {
        if (diagnostic?.Kind is not { } kind)
        {
            return null;
        }

        return new DaemonPrimaryDiagnosticOutput(
            Kind: kind,
            Code: StringValueNormalizer.TrimToNull(diagnostic.Code),
            File: StringValueNormalizer.TrimToNull(diagnostic.File),
            Line: diagnostic.Line,
            Column: diagnostic.Column,
            Message: StringValueNormalizer.TrimToNull(diagnostic.Message));
    }
}
