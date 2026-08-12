using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Provides the <c>play exit</c> CLI command entry point. </summary>
internal sealed class PlayExitCommand
{
    private readonly IPlayExitService playExitService;

    private readonly ICommandResultWriter commandResultWriter;

    private readonly IPlayLifecycleExecutionStartInvocationFactory invocationFactory;

    /// <summary> Initializes a new instance of the <see cref="PlayExitCommand" /> class. </summary>
    /// <param name="playExitService"> The Play Mode exit service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public PlayExitCommand (
        IPlayExitService playExitService,
        ICommandResultWriter commandResultWriter,
        IPlayLifecycleExecutionStartInvocationFactory invocationFactory)
    {
        this.playExitService = playExitService ?? throw new ArgumentNullException(nameof(playExitService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
        this.invocationFactory = invocationFactory ?? throw new ArgumentNullException(nameof(invocationFactory));
    }

    /// <summary> Requests Unity to exit Play Mode and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ExitSubcommand)]
    public async Task<int> ExitAsync (
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var timeoutNormalizationResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutNormalizationResult.IsSuccess)
        {
            var invalidTimeoutResult =
                PlayExitCommandResultFactory.CreateExecutionError(
                    timeoutNormalizationResult.Error!);
            commandResultWriter.WriteToStandardOutput(invalidTimeoutResult);
            return invalidTimeoutResult.ExitCode;
        }

        var invocationResult = await invocationFactory.CreateExitAsync(
                projectPath,
                timeoutNormalizationResult.TimeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        PlayExitExecutionResult executionResult;
        if (invocationResult.IsSuccess)
        {
            var invocation = invocationResult.Invocation!;
            await using var hostBinding = invocation.Context.HostBinding;
            executionResult = await playExitService.StartAsync(
                    invocation,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            executionResult = PlayExitExecutionResult.Failure(invocationResult.Failure!);
        }
        var commandResult = PlayExitCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
