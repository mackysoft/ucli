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

/// <summary> Converts daemon-cleanup skip-reason enum to command contract literals. </summary>
internal static class DaemonCleanupSkipReasonCodec
{
    /// <summary> Gets the skip-reason literal used when daemon is running. </summary>
    public const string Running = "running";

    /// <summary> Gets the skip-reason literal used when invalid session may still belong to a live daemon. </summary>
    public const string UnsafeInvalidSession = "unsafeInvalidSession";

    /// <summary> Gets the skip-reason literal used when daemon reachability is uncertain. </summary>
    public const string UncertainReachability = "uncertainReachability";

    /// <summary> Converts daemon-cleanup skip-reason enum to command contract literal. </summary>
    /// <param name="skipReason"> The daemon-cleanup skip-reason enum value. </param>
    /// <param name="value"> The converted skip-reason literal when conversion succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when conversion succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryToValue (
        DaemonCleanupSkipReason skipReason,
        [NotNullWhen(true)]
        out string? value)
    {
        value = skipReason switch
        {
            DaemonCleanupSkipReason.None => null,
            DaemonCleanupSkipReason.Running => Running,
            DaemonCleanupSkipReason.UnsafeInvalidSession => UnsafeInvalidSession,
            DaemonCleanupSkipReason.UncertainReachability => UncertainReachability,
            _ => null,
        };
        return skipReason == DaemonCleanupSkipReason.None || value is not null;
    }
}
