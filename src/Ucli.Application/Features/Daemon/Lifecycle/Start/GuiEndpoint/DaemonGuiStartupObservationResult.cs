using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Represents the bounded observation result for one CLI-launched GUI daemon startup attempt. </summary>
internal sealed record DaemonGuiStartupObservationResult
{
    private DaemonGuiStartupObservationResult (
        DaemonSession? session,
        IpcUnityEditorObservation? lifecycleObservation,
        DaemonGuiStartupBlockerObservation? blockerObservation,
        ExecutionError? error)
    {
        Session = session;
        LifecycleObservation = lifecycleObservation;
        BlockerObservation = blockerObservation;
        Error = error;
    }

    /// <summary> Gets the registered GUI daemon session when startup succeeds. </summary>
    public DaemonSession? Session { get; }

    /// <summary> Gets the endpoint-registered lifecycle observation when startup succeeds. </summary>
    public IpcUnityEditorObservation? LifecycleObservation { get; }

    /// <summary> Gets the terminal startup blocker observation when one is available. </summary>
    public DaemonGuiStartupBlockerObservation? BlockerObservation { get; }

    /// <summary> Gets the structured error when observation fails or times out. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether GUI daemon startup succeeded. </summary>
    [MemberNotNullWhen(true, nameof(Session), nameof(LifecycleObservation))]
    public bool IsSuccess => Session is not null && LifecycleObservation is not null && BlockerObservation is null && Error is null;

    /// <summary> Gets a value indicating whether GUI daemon startup was blocked by a known terminal condition. </summary>
    [MemberNotNullWhen(true, nameof(BlockerObservation))]
    public bool IsBlocked => Session is null && BlockerObservation is not null && Error is null;

    /// <summary> Creates a successful observation result. </summary>
    public static DaemonGuiStartupObservationResult Success (
        DaemonSession session,
        IpcUnityEditorObservation lifecycleObservation)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lifecycleObservation);
        return new DaemonGuiStartupObservationResult(session, lifecycleObservation, null, null);
    }

    /// <summary> Creates a blocked observation result. </summary>
    public static DaemonGuiStartupObservationResult Blocked (DaemonGuiStartupBlockerObservation blockerObservation)
    {
        ArgumentNullException.ThrowIfNull(blockerObservation);
        return new DaemonGuiStartupObservationResult(null, null, blockerObservation, null);
    }

    /// <summary> Creates a failed observation result. </summary>
    public static DaemonGuiStartupObservationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonGuiStartupObservationResult(null, null, null, error);
    }
}
