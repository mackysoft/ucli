using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;

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
