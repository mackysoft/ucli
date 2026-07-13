using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Represents daemon startup endpoint-registration probing result. </summary>
internal sealed record DaemonStartupReadinessProbeResult
{
    private DaemonStartupReadinessProbeResult (
        ExecutionError? error,
        IpcUnityEditorObservation? lifecycleObservation,
        DaemonStartupFailureClassification? failureClassification)
    {
        Error = error;
        LifecycleObservation = lifecycleObservation;
        FailureClassification = failureClassification;
    }

    /// <summary> Gets whether daemon endpoint registration probing succeeded. </summary>
    [MemberNotNullWhen(true, nameof(LifecycleObservation))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsReady => LifecycleObservation is not null;

    /// <summary> Gets the structured error when probing failed; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets the endpoint-registered lifecycle observation when probing succeeded; otherwise <see langword="null" />. </summary>
    public IpcUnityEditorObservation? LifecycleObservation { get; }

    /// <summary> Gets the classified startup blocker when probing failed with a known blocker; otherwise <see langword="null" />. </summary>
    public DaemonStartupFailureClassification? FailureClassification { get; }

    /// <summary> Creates a successful endpoint-registration probe result. </summary>
    /// <param name="lifecycleObservation"> The endpoint-registered lifecycle observation. </param>
    /// <returns> The successful endpoint-registration probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="lifecycleObservation" /> is <see langword="null" />. </exception>
    public static DaemonStartupReadinessProbeResult Ready (IpcUnityEditorObservation lifecycleObservation)
    {
        ArgumentNullException.ThrowIfNull(lifecycleObservation);
        return new DaemonStartupReadinessProbeResult(
            error: null,
            lifecycleObservation,
            failureClassification: null);
    }

    /// <summary> Creates a failed endpoint-registration probe result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed readiness-probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStartupReadinessProbeResult Failure (
        ExecutionError error,
        DaemonStartupFailureClassification? failureClassification = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStartupReadinessProbeResult(
            error,
            lifecycleObservation: null,
            failureClassification);
    }
}
