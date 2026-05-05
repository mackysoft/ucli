using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

namespace MackySoft.Ucli.Features.Daemon.Common.CommandContracts;

/// <summary> Converts daemon-list completion reason enum to command contract literals. </summary>
internal static class DaemonListCompletionReasonCodec
{
    public const string Timeout = "timeout";

    public static bool TryToValue (
        DaemonListCompletionReason reason,
        [NotNullWhen(true)]
        out string? value)
    {
        value = reason switch
        {
            DaemonListCompletionReason.Timeout => Timeout,
            _ => null,
        };
        return value is not null;
    }
}
