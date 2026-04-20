using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Features.Daemon.UseCases.Common;

/// <summary> Resolves daemon-command preflight values from project context and timeout options. </summary>
internal sealed class DaemonCommandExecutionContextResolver : IDaemonCommandExecutionContextResolver
{
    private readonly IProjectContextResolver projectContextResolver;

    /// <summary> Initializes a new instance of the <see cref="DaemonCommandExecutionContextResolver" /> class. </summary>
    /// <param name="projectContextResolver"> The shared project-context resolver dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectContextResolver" /> is <see langword="null" />. </exception>
    public DaemonCommandExecutionContextResolver (IProjectContextResolver projectContextResolver)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
    }

    /// <summary> Resolves project context and timeout values for one daemon subcommand execution. </summary>
    /// <param name="timeoutCommand"> The timeout-config command key used to resolve default timeout. </param>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-command execution-context resolution result. </returns>
    public async ValueTask<DaemonCommandExecutionContextResolutionResult> Resolve (
        UcliCommand timeoutCommand,
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        if (!timeoutCommand.IsValid)
        {
            throw new ArgumentException("Timeout command name is invalid.", nameof(timeoutCommand));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var contextResolutionResult = await projectContextResolver.Resolve(projectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResolutionResult.IsSuccess)
        {
            return DaemonCommandExecutionContextResolutionResult.Failure(contextResolutionResult.Error!);
        }

        var context = contextResolutionResult.Context!;
        var timeoutResolutionResult = IpcCommandTimeoutResolver.Resolve(timeout, timeoutCommand, context.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return DaemonCommandExecutionContextResolutionResult.Failure(timeoutResolutionResult.Error!);
        }

        return DaemonCommandExecutionContextResolutionResult.Success(new DaemonCommandExecutionContext(
            Context: context,
            Timeout: timeoutResolutionResult.Timeout!.Value));
    }
}