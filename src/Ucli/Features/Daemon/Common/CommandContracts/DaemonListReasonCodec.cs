using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

namespace MackySoft.Ucli.Features.Daemon.Common.CommandContracts;

/// <summary> Converts daemon-list item reason enum to command contract literals. </summary>
internal static class DaemonListReasonCodec
{
    public const string StaleSession = "staleSession";

    public const string InvalidSession = "invalidSession";

    public const string ProbeTimeout = "probeTimeout";

    public const string ProbeFailed = "probeFailed";

    public static bool TryToValue (
        DaemonListItemReason reason,
        [NotNullWhen(true)]
        out string? value)
    {
        value = reason switch
        {
            DaemonListItemReason.StaleSession => StaleSession,
            DaemonListItemReason.InvalidSession => InvalidSession,
            DaemonListItemReason.ProbeTimeout => ProbeTimeout,
            DaemonListItemReason.ProbeFailed => ProbeFailed,
            _ => null,
        };
        return value is not null;
    }
}
