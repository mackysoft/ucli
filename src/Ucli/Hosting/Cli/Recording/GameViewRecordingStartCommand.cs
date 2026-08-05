using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Recording.UseCases;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;
using MackySoft.Ucli.Hosting.Cli.Recording.Input;

namespace MackySoft.Ucli.Hosting.Cli.Recording;

/// <summary>Provides the <c>recording start</c> CLI entry point.</summary>
internal sealed class GameViewRecordingStartCommand
{
    private readonly IGameViewRecordingService recordingService;
    private readonly IGameViewRecordingRequestInputReader requestInputReader;
    private readonly ICommandResultWriter commandResultWriter;

    public GameViewRecordingStartCommand (
        IGameViewRecordingService recordingService,
        IGameViewRecordingRequestInputReader requestInputReader,
        ICommandResultWriter commandResultWriter)
    {
        this.recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        this.requestInputReader = requestInputReader ?? throw new ArgumentNullException(nameof(requestInputReader));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary>Starts one GameView recording from a JSON request and emits its durable execution.</summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="requestPath">--requestPath, Optional request JSON path. Omit it when reading redirected standard input.</param>
    /// <param name="recordingId">--recordingId, Optional non-zero UUID used to retry the same logical recording.</param>
    /// <param name="detach">Return after the recording starts instead of monitoring it to terminal state.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token propagated by command execution.</param>
    /// <returns>The exit code contained in the emitted command result.</returns>
    [Command(UcliCommandNames.StartSubcommand)]
    public async Task<int> StartAsync (
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        [AbsolutePathArgumentParser] AbsolutePath? requestPath = null,
        [OptionalRecordingIdArgumentParser] Guid? recordingId = null,
        bool detach = false,
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

        var inputResult = await requestInputReader
            .ReadAsync(requestPath, cancellationToken)
            .ConfigureAwait(false);
        if (!inputResult.IsSuccess)
        {
            return WriteError(inputResult.Error!);
        }

        var serviceResult = await recordingService.StartAsync(
                new GameViewRecordingStartInput(
                    ProjectPath: projectPath,
                    RequestJson: inputResult.Json!,
                    RecordingId: recordingId,
                    Detach: detach,
                    TimeoutMilliseconds: timeoutResult.TimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = GameViewRecordingCommandResultFactory.CreateStart(serviceResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    private int WriteError (ExecutionError error)
    {
        var result = GameViewRecordingCommandResultFactory.CreateExecutionError(
            UcliCommandNames.RecordingStart,
            error);
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}
