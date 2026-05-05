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

/// <summary> Converts daemon-cleanup status enum to command contract literals. </summary>
internal static class DaemonCleanupStateCodec
{
    /// <summary> Gets the cleanup-status value used when cleanup completed. </summary>
    public const string Completed = "completed";

    /// <summary> Gets the cleanup-status value used when cleanup was skipped. </summary>
    public const string Skipped = "skipped";

    /// <summary> Converts daemon-cleanup status enum to command contract literal. </summary>
    /// <param name="status"> The daemon-cleanup status enum value. </param>
    /// <param name="value"> The converted cleanup-status literal when conversion succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when conversion succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryToValue (
        DaemonCleanupStatus status,
        [NotNullWhen(true)]
        out string? value)
    {
        value = status switch
        {
            DaemonCleanupStatus.Completed => Completed,
            DaemonCleanupStatus.Skipped => Skipped,
            _ => null,
        };
        return value is not null;
    }
}
