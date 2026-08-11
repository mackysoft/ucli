using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Provides the <c>play enter</c> CLI command entry point. </summary>
internal sealed class PlayEnterCommand
{
    private readonly IPlayEnterService playEnterService;

    private readonly ICommandResultWriter commandResultWriter;

    private readonly ILifecycleExecutionCliInvocationFactory invocationFactory;

    /// <summary> Initializes a new instance of the <see cref="PlayEnterCommand" /> class. </summary>
    /// <param name="playEnterService"> The Play Mode enter service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public PlayEnterCommand (
        IPlayEnterService playEnterService,
        ICommandResultWriter commandResultWriter,
        ILifecycleExecutionCliInvocationFactory invocationFactory)
    {
        this.playEnterService = playEnterService ?? throw new ArgumentNullException(nameof(playEnterService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
        this.invocationFactory = invocationFactory ?? throw new ArgumentNullException(nameof(invocationFactory));
    }

    /// <summary> Requests Unity to enter Play Mode and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.EnterSubcommand)]
    public async Task<int> EnterAsync (
        string? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var timeoutNormalizationResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutNormalizationResult.IsSuccess)
        {
            var invalidTimeoutResult =
                PlayEnterCommandResultFactory.CreateExecutionError(
                    timeoutNormalizationResult.Error!);
            commandResultWriter.WriteToStandardOutput(invalidTimeoutResult);
            return invalidTimeoutResult.ExitCode;
        }

        var invocationResult = await invocationFactory.CreatePlayEnterStartAsync(
                projectPath,
                timeoutNormalizationResult.TimeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        var executionResult = invocationResult.IsSuccess
            ? await playEnterService.StartAsync(
                    invocationResult.Invocation!,
                    cancellationToken)
                .ConfigureAwait(false)
            : PlayEnterExecutionResult.Failure(invocationResult.Failure!);
        var commandResult = PlayEnterCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
