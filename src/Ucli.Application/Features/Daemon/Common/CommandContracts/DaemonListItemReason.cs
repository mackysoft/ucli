namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Identifies why one daemon-list item is not reported as running. </summary>
internal enum DaemonListItemReason
{
    StaleSession,
    InvalidSession,
    ProbeTimeout,
    ProbeFailed,
}
