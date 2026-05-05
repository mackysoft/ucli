using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Defines process identity assessment states for daemon-session cleanup. </summary>
internal enum DaemonProcessIdentityAssessmentStatus
{
    /// <summary> Indicates the target process is not running. </summary>
    NotRunning = 0,

    /// <summary> Indicates the target process is running and matches expected daemon identity. </summary>
    MatchingLiveProcess = 1,

    /// <summary> Indicates the target process is live but does not match the expected daemon identity. </summary>
    DifferentProcess = 2,

    /// <summary> Indicates the target process identity could not be assessed. </summary>
    Uncertain = 3,
}
