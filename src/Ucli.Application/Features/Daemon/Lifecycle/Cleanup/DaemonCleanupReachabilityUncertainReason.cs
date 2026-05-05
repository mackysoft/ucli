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
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Represents one known reason why cleanup reachability could not safely prove daemon absence. </summary>
internal enum DaemonCleanupReachabilityUncertainReason
{
    /// <summary> The ping request timed out after connection, so cleanup cannot distinguish a hung daemon from daemon absence safely. </summary>
    Timeout = 0,

    /// <summary> The IPC transport could not finish connecting before timeout, which does not prove the canonical endpoint is absent. </summary>
    ConnectTimeout = 1,

    /// <summary> The endpoint responded but rejected cleanup probe authentication, so destructive cleanup must not treat the endpoint as absent. </summary>
    SessionAuthenticationRejected = 2,

    /// <summary> The transport failed without direct endpoint absence evidence, so cleanup must remain non-destructive. </summary>
    TransportError = 3,
}
