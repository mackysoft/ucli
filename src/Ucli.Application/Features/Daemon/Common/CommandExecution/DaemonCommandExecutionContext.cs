using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;

/// <summary> Represents normalized preflight values for one daemon subcommand execution. </summary>
/// <param name="Context"> The resolved shared project context. </param>
/// <param name="Timeout"> The effective timeout used for daemon management operations. </param>
internal sealed record DaemonCommandExecutionContext (
    ProjectContext Context,
    TimeSpan Timeout);
