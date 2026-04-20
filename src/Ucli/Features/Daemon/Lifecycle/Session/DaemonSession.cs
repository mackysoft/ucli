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
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Session;

/// <summary> Represents persisted daemon session metadata bound to one project fingerprint. </summary>
/// <param name="SchemaVersion"> The schema version used for JSON persistence compatibility. </param>
/// <param name="SessionToken"> The daemon session token used for IPC authorization. </param>
/// <param name="ProjectFingerprint"> The project fingerprint associated with this daemon session. </param>
/// <param name="IssuedAtUtc"> The UTC timestamp when this session was issued. </param>
/// <param name="RuntimeKind"> The daemon runtime kind. </param>
/// <param name="OwnerKind"> The daemon owner kind. </param>
/// <param name="CanShutdownProcess"> Whether daemon management is allowed to shutdown the process. </param>
/// <param name="EndpointTransportKind"> The transport kind string used by daemon endpoint. </param>
/// <param name="EndpointAddress"> The endpoint address string used by daemon endpoint. </param>
/// <param name="ProcessId"> The daemon process identifier when available. </param>
/// <param name="OwnerProcessId"> The owner process identifier when available. </param>
internal sealed record DaemonSession (
    int SchemaVersion,
    string SessionToken,
    string ProjectFingerprint,
    DateTimeOffset IssuedAtUtc,
    string RuntimeKind,
    string OwnerKind,
    bool CanShutdownProcess,
    string EndpointTransportKind,
    string EndpointAddress,
    int? ProcessId,
    int? OwnerProcessId)
{
    /// <summary> Gets the schema version used by daemon session persistence. </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary> Gets the runtime kind value used for CLI-started batchmode daemon sessions. </summary>
    public const string RuntimeKindBatchmode = "batchmode";

    /// <summary> Gets the owner kind value used for supervisor-managed daemon sessions. </summary>
    public const string OwnerKindSupervisor = "supervisor";
}