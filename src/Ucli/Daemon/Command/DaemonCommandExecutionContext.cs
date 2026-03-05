using MackySoft.Ucli.Context;

namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Represents normalized preflight values for one daemon subcommand execution. </summary>
/// <param name="Context"> The resolved init/status context. </param>
/// <param name="Timeout"> The effective timeout used for daemon management operations. </param>
internal sealed record DaemonCommandExecutionContext (
    InitStatusContext Context,
    TimeSpan Timeout);