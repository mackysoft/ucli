using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;
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
            CompileGeneration: null,
            DomainReloadGeneration: null,
            CanAcceptExecutionRequests: false,
            Runtime: null);
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

        return new StatusDaemonObservation(
            DaemonStatus: daemonStatus,
            ServerVersion: StringValueNormalizer.TrimToNull(pingResponse.ServerVersion),
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileState: IpcCompileStateCodec.TryParse(pingResponse.CompileState, out var compileState)
                ? compileState
                : null,
            CompileGeneration: StringValueNormalizer.TrimToNull(pingResponse.CompileGeneration),
            DomainReloadGeneration: StringValueNormalizer.TrimToNull(pingResponse.DomainReloadGeneration),
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            Runtime: IpcEditorRuntimeCodec.TryParse(pingResponse.Runtime, out var runtime)
                ? runtime
                : null);
    }
}
