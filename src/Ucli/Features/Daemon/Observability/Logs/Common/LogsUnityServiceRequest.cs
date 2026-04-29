using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;

/// <summary> Represents normalized option values for one <c>logs unity</c> command execution. </summary>
/// <param name="ProjectPath"> The optional Unity project root path. </param>
/// <param name="Tail"> The optional tail count. </param>
/// <param name="After"> The optional opaque cursor used for incremental reads. </param>
/// <param name="Since"> The optional lower time bound in ISO 8601 format. </param>
/// <param name="Until"> The optional upper time bound in ISO 8601 format. </param>
/// <param name="Level"> The optional level filter (<c>error|warning|info|all</c>). </param>
/// <param name="Query"> The optional free-text query value. </param>
/// <param name="QueryTarget"> The optional query target value. </param>
/// <param name="Source"> The optional source filter. </param>
/// <param name="StackTrace"> The optional stack-trace mode. </param>
/// <param name="StackTraceMaxFrames"> The optional maximum number of stack-trace frames. </param>
/// <param name="StackTraceMaxChars"> The optional maximum number of stack-trace characters. </param>
/// <param name="Stream"> Indicates whether command should continue stream polling. </param>
/// <param name="PollIntervalMilliseconds"> The optional stream polling interval in milliseconds. </param>
/// <param name="IdleTimeoutMilliseconds"> The optional stream idle-timeout threshold in milliseconds. </param>
internal sealed record LogsUnityServiceRequest (
    string? ProjectPath,
    int? Tail,
    string? After,
    string? Since,
    string? Until,
    string? Level,
    string? Query,
    string? QueryTarget,
    string? Source,
    string? StackTrace,
    int? StackTraceMaxFrames,
    int? StackTraceMaxChars,
    bool Stream,
    int? PollIntervalMilliseconds,
    int? IdleTimeoutMilliseconds);
