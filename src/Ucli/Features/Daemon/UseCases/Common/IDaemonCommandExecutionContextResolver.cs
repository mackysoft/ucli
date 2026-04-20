using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Features.Daemon.UseCases.Common;

/// <summary> Resolves shared execution-context values for daemon command workflows. </summary>
internal interface IDaemonCommandExecutionContextResolver
{
    /// <summary> Resolves project context and timeout values for one daemon subcommand execution. </summary>
    /// <param name="timeoutCommand"> The timeout-config command key used to resolve default timeout. </param>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-command execution-context resolution result. </returns>
    ValueTask<DaemonCommandExecutionContextResolutionResult> Resolve (
        UcliCommand timeoutCommand,
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default);
}