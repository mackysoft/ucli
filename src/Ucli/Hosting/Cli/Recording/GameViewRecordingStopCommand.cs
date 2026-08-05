using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Recording.UseCases;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Recording;

/// <summary>Provides the <c>recording stop</c> CLI entry point.</summary>
internal sealed class GameViewRecordingStopCommand
{
    private readonly IGameViewRecordingService recordingService;
    private readonly ICommandResultWriter commandResultWriter;

    public GameViewRecordingStopCommand (
        IGameViewRecordingService recordingService,
        ICommandResultWriter commandResultWriter)
    {
        this.recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary>Requests an idempotent stop and emits the recording recovery or terminal state.</summary>
    /// <param name="recordingId">--recordingId, Required non-zero UUID selecting the recording to stop.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token propagated by command execution.</param>
    /// <returns>The exit code contained in the emitted command result.</returns>
    [Command(UcliCommandNames.StopSubcommand)]
    public async Task<int> StopAsync (
        [RecordingIdArgumentParser] Guid recordingId,
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            return WriteError(timeoutResult.Error!);
        }

        var serviceResult = await recordingService.StopAsync(
                new GameViewRecordingStopInput(
                    ProjectPath: projectPath,
                    RecordingId: recordingId,
                    TimeoutMilliseconds: timeoutResult.TimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = GameViewRecordingCommandResultFactory.CreateStop(serviceResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    private int WriteError (ExecutionError error)
    {
        var result = GameViewRecordingCommandResultFactory.CreateExecutionError(
            UcliCommandNames.RecordingStop,
            error);
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}
