using MackySoft.Ucli.Shared.Context;

namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Represents normalized preflight values for one daemon subcommand execution. </summary>
/// <param name="Context"> The resolved shared project context. </param>
/// <param name="Timeout"> The effective timeout used for daemon management operations. </param>
internal sealed record DaemonCommandExecutionContext (
    ProjectContext Context,
    TimeSpan Timeout);