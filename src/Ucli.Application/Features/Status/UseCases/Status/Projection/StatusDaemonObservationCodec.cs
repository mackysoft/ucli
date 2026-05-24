using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.CommandContracts.Projection;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
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
            CompileGeneration: null,
            DomainReloadGeneration: null,
            CanAcceptExecutionRequests: false,
            EditorMode: null,
            ObservedAtUtc: null,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: null);
    }

    /// <summary> Creates observation values from a persisted lifecycle sidecar when ping details are unavailable. </summary>
    public static StatusDaemonObservation CreateFromLifecycleObservation (
        DaemonStatusKind daemonStatus,
        DaemonLifecycleObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        return new StatusDaemonObservation(
            DaemonStatus: daemonStatus,
            ServerVersion: observation.ServerVersion,
            LifecycleState: observation.LifecycleState,
            BlockingReason: observation.BlockingReason,
            CompileState: observation.CompileState,
            CompileGeneration: observation.CompileGeneration,
            DomainReloadGeneration: observation.DomainReloadGeneration,
            CanAcceptExecutionRequests: observation.CanAcceptExecutionRequests,
            EditorMode: observation.EditorMode,
            ObservedAtUtc: observation.ObservedAtUtc,
            ActionRequired: observation.ActionRequired,
            PrimaryDiagnostic: ToOutput(observation.PrimaryDiagnostic),
            PlayMode: PlayModeSnapshotOutputFactory.Create(observation.PlayMode));
    }

    /// <summary> Creates observation values for an unreachable daemon whose lifecycle cannot be inferred. </summary>
    public static StatusDaemonObservation CreateUnavailable (DaemonStatusKind daemonStatus)
    {
        return new StatusDaemonObservation(
            DaemonStatus: daemonStatus,
            ServerVersion: null,
            LifecycleState: IpcEditorLifecycleStateCodec.Unavailable,
            BlockingReason: IpcEditorBlockingReasonCodec.Unavailable,
            CompileState: null,
            CompileGeneration: null,
            DomainReloadGeneration: null,
            CanAcceptExecutionRequests: false,
            EditorMode: null,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
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
        IpcPingResponse pingResponse)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        var projection = LifecycleProjectionFactory.Create(pingResponse);

        return new StatusDaemonObservation(
            DaemonStatus: daemonStatus,
            ServerVersion: projection.ServerVersion,
            LifecycleState: projection.LifecycleState,
            BlockingReason: projection.BlockingReason,
            CompileState: projection.CompileState,
            CompileGeneration: projection.CompileGeneration,
            DomainReloadGeneration: projection.DomainReloadGeneration,
            CanAcceptExecutionRequests: projection.CanAcceptExecutionRequests,
            EditorMode: projection.EditorMode,
            ObservedAtUtc: projection.ObservedAtUtc,
            ActionRequired: projection.ActionRequired,
            PrimaryDiagnostic: ToOutput(projection.PrimaryDiagnostic),
            PlayMode: projection.PlayMode);
    }

    private static DaemonPrimaryDiagnosticOutput? ToOutput (IpcPrimaryDiagnostic? diagnostic)
    {
        if (diagnostic is null || !StringValueNormalizer.TryTrimToNonEmpty(diagnostic.Kind, out var kind))
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
