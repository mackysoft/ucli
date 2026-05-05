namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Identifies one daemon-list item observation state. </summary>
internal enum DaemonListItemState
{
    Running,
    Stale,
    Error,
}
