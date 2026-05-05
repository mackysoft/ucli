using System.Diagnostics.CodeAnalysis;
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
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Converts daemon-start status enum to daemon command contract literals. </summary>
internal static class DaemonStartStateCodec
{
    /// <summary> Gets the start-status value used when daemon process was started. </summary>
    public const string Started = "started";

    /// <summary> Gets the start-status value used when daemon is already running. </summary>
    public const string AlreadyRunning = "alreadyRunning";

    /// <summary> Converts daemon-start status enum to command contract literal. </summary>
    /// <param name="status"> The daemon-start status enum value. </param>
    /// <param name="value"> The converted start-status literal when conversion succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when conversion succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryToValue (
        DaemonStartStatus status,
        [NotNullWhen(true)]
        out string? value)
    {
        value = status switch
        {
            DaemonStartStatus.Started => Started,
            DaemonStartStatus.AlreadyRunning => AlreadyRunning,
            _ => null,
        };
        return value is not null;
    }
}
