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
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Represents normalized session payload values returned by daemon command workflows. </summary>
/// <param name="ProjectFingerprint"> The project fingerprint associated with daemon session. </param>
/// <param name="IssuedAtUtc"> The UTC timestamp when daemon session was issued. </param>
/// <param name="RuntimeKind"> The daemon runtime kind. </param>
/// <param name="OwnerKind"> The daemon owner kind. </param>
/// <param name="CanShutdownProcess"> Whether daemon session allows process shutdown. </param>
/// <param name="EndpointTransportKind"> The IPC endpoint transport kind literal. </param>
/// <param name="EndpointAddress"> The IPC endpoint address literal. </param>
/// <param name="ProcessId"> The daemon process identifier when available; otherwise <see langword="null" />. </param>
/// <param name="OwnerProcessId"> The owner process identifier when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonSessionOutput (
    string ProjectFingerprint,
    DateTimeOffset IssuedAtUtc,
    string RuntimeKind,
    string OwnerKind,
    bool CanShutdownProcess,
    string EndpointTransportKind,
    string EndpointAddress,
    int? ProcessId,
    int? OwnerProcessId);
