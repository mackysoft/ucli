using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Contracts.Ipc;
namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;

/// <summary> Represents normalized payload values for one daemon-start command execution. </summary>
/// <param name="StartStatus"> The daemon-start outcome value (<c>started</c> or <c>alreadyRunning</c>). </param>
/// <param name="DaemonStatus"> The daemon-status value. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon start workflow. </param>
/// <param name="Session"> The daemon session values associated with started or already-running daemon process. </param>
/// <param name="LifecycleState"> The lifecycle-state snapshot observed after endpoint registration. </param>
/// <param name="BlockingReason"> The blocking-reason snapshot observed after endpoint registration. </param>
/// <param name="CanAcceptExecutionRequests"> Whether the endpoint can accept ordinary execution requests. </param>
internal sealed record DaemonStartExecutionOutput (
    DaemonStartStatus StartStatus,
    DaemonStatusKind DaemonStatus,
    int TimeoutMilliseconds,
    DaemonSessionOutput Session,
    string LifecycleState = IpcEditorLifecycleStateCodec.Ready,
    string? BlockingReason = null,
    bool CanAcceptExecutionRequests = true);
