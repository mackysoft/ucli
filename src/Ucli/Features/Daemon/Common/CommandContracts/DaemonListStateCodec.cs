using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

namespace MackySoft.Ucli.Features.Daemon.Common.CommandContracts;

/// <summary> Converts daemon-list item state enum to command contract literals. </summary>
internal static class DaemonListStateCodec
{
    public const string Running = "running";

    public const string Stale = "stale";

    public const string Error = "error";

    public static bool TryToValue (
        DaemonListItemState state,
        [NotNullWhen(true)]
        out string? value)
    {
        value = state switch
        {
            DaemonListItemState.Running => Running,
            DaemonListItemState.Stale => Stale,
            DaemonListItemState.Error => Error,
            _ => null,
        };
        return value is not null;
    }
}
