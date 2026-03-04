using MackySoft.Ucli.Context;
using MackySoft.Ucli.Execution;

namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Resolves daemon-command preflight values from project context and timeout options. </summary>
internal sealed class DaemonCommandExecutionContextResolver : IDaemonCommandExecutionContextResolver
{
    private const string DaemonCommandName = "daemon";

    private readonly IInitStatusContextResolver initStatusContextResolver;

    /// <summary> Initializes a new instance of the <see cref="DaemonCommandExecutionContextResolver" /> class. </summary>
    /// <param name="initStatusContextResolver"> The init/status context resolver dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="initStatusContextResolver" /> is <see langword="null" />. </exception>
    public DaemonCommandExecutionContextResolver (IInitStatusContextResolver initStatusContextResolver)
    {
        this.initStatusContextResolver = initStatusContextResolver ?? throw new ArgumentNullException(nameof(initStatusContextResolver));
    }

    /// <summary> Resolves project context and timeout values for one daemon subcommand execution. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-command execution-context resolution result. </returns>
    public async ValueTask<DaemonCommandExecutionContextResolutionResult> Resolve (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResolutionResult = await initStatusContextResolver.Resolve(projectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResolutionResult.IsSuccess)
        {
            return DaemonCommandExecutionContextResolutionResult.Failure(contextResolutionResult.Error!);
        }

        var context = contextResolutionResult.Context!;
        var timeoutResolutionResult = IpcCommandTimeoutResolver.Resolve(timeout, DaemonCommandName, context.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return DaemonCommandExecutionContextResolutionResult.Failure(timeoutResolutionResult.Error!);
        }

        return DaemonCommandExecutionContextResolutionResult.Success(new DaemonCommandExecutionContext(
            Context: context,
            Timeout: timeoutResolutionResult.Timeout!.Value));
    }
}