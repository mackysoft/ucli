using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Status.UseCases.Status.Projection;

/// <summary> Converts daemon status and ping payload values to status command observation contract values. </summary>
internal static class StatusDaemonObservationCodec
{
    /// <summary> Creates observation values for daemon states where ping details are unavailable. </summary>
    /// <param name="daemonStatus"> The daemon status enum value. </param>
    /// <returns> The observation values with null ping fields. </returns>
    public static StatusDaemonObservation CreateWithoutPing (DaemonStatusKind daemonStatus)
    {
        return new StatusDaemonObservation(
            DaemonStatus: daemonStatus,
            ServerVersion: null,
            LifecycleState: null,
            BlockingReason: null,
            CompileState: null,
            Generations: null,
            CanAcceptExecutionRequests: false,
            EditorMode: null,
            ObservedAtUtc: null,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: null);
    }

    /// <summary> Creates observation values for daemon states where ping details are available. </summary>
    /// <param name="daemonStatus"> The daemon status enum value. </param>
    /// <param name="pingResponse"> The ping response payload. </param>
    /// <returns> The observation values projected for status payload. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pingResponse" /> is <see langword="null" />. </exception>
    public static StatusDaemonObservation CreateFromPing (
        DaemonStatusKind daemonStatus,
        IpcUnityEditorObservation pingResponse)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        var state = pingResponse.State;

        return new StatusDaemonObservation(
            DaemonStatus: daemonStatus,
            ServerVersion: StringValueNormalizer.TrimToNull(pingResponse.ServerVersion),
            LifecycleState: state.LifecycleState,
            BlockingReason: IpcEditorLifecycleSemantics.ResolveBlockingReason(state.LifecycleState),
            CompileState: state.CompileState,
            Generations: state.Generations,
            CanAcceptExecutionRequests: IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(state.LifecycleState),
            EditorMode: state.EditorMode,
            ObservedAtUtc: pingResponse.ObservedAtUtc,
            ActionRequired: pingResponse.ActionRequired,
            PrimaryDiagnostic: ToOutput(pingResponse.PrimaryDiagnostic),
            PlayMode: state.PlayMode);
    }

    private static DaemonPrimaryDiagnosticOutput? ToOutput (IpcPrimaryDiagnostic? diagnostic)
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
