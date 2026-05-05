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
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Converts daemon-status enum to daemon command contract literals. </summary>
internal static class DaemonStatusStateCodec
{
    /// <summary> Gets the daemon-status value used when daemon is running. </summary>
    public const string Running = "running";

    /// <summary> Gets the daemon-status value used when daemon is not running. </summary>
    public const string NotRunning = "notRunning";

    /// <summary> Gets the daemon-status value used when daemon session is stale. </summary>
    public const string Stale = "stale";

    /// <summary> Converts daemon-status enum to command contract literal. </summary>
    /// <param name="status"> The daemon-status enum value. </param>
    /// <param name="value"> The converted daemon-status literal when conversion succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when conversion succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryToValue (
        DaemonStatusKind status,
        [NotNullWhen(true)]
        out string? value)
    {
        value = status switch
        {
            DaemonStatusKind.Running => Running,
            DaemonStatusKind.NotRunning => NotRunning,
            DaemonStatusKind.Stale => Stale,
            _ => null,
        };
        return value is not null;
    }
}
