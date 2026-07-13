using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Represents the result of waiting for a GUI daemon session registration. </summary>
internal sealed record DaemonGuiSessionRegistrationWaitResult
{
    private DaemonGuiSessionRegistrationWaitResult (
        DaemonSession? session,
        IpcUnityEditorObservation? lifecycleObservation,
        ExecutionError? error)
    {
        Session = session;
        LifecycleObservation = lifecycleObservation;
        Error = error;
    }

    /// <summary> Gets the matching GUI daemon session for a successful result; otherwise <see langword="null" />. </summary>
    public DaemonSession? Session { get; }

    /// <summary> Gets the endpoint-registered lifecycle observation for a successful result; otherwise <see langword="null" />. </summary>
    public IpcUnityEditorObservation? LifecycleObservation { get; }

    /// <summary> Gets the structured wait error for a failed result; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether the wait succeeded. </summary>
    [MemberNotNullWhen(true, nameof(Session), nameof(LifecycleObservation))]
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful GUI session wait result. </summary>
    /// <param name="session"> The registered GUI daemon session. </param>
    /// <param name="lifecycleObservation"> The endpoint-registered lifecycle observation. </param>
    /// <returns> The successful wait result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> or <paramref name="lifecycleObservation" /> is <see langword="null" />. </exception>
    public static DaemonGuiSessionRegistrationWaitResult Success (
        DaemonSession session,
        IpcUnityEditorObservation lifecycleObservation)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lifecycleObservation);
        return new DaemonGuiSessionRegistrationWaitResult(session, lifecycleObservation, null);
    }

    /// <summary> Creates a failed GUI session wait result. </summary>
    /// <param name="error"> The structured wait error. </param>
    /// <returns> The failed wait result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonGuiSessionRegistrationWaitResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonGuiSessionRegistrationWaitResult(null, null, error);
    }
}
