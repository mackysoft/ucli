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
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

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
